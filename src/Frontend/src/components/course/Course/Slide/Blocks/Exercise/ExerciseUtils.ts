import {
	AutomaticExerciseCheckingResult,
	AutomaticExerciseCheckingResult as CheckingResult,
	SolutionRunStatus,
	SubmissionInfo
} from "src/models/exercise";
import { ReviewInfoRedux, SubmissionInfoRedux } from "src/models/reduxState";
import { Language } from "src/consts/languages";
import CodeMirror, { Doc, MarkerRange, TextMarker } from "codemirror";

enum SubmissionColor {
	MaxResult = "MaxResult", // Студенту больше ничего не может сделать, ни сийчам ни в будущем
	NeedImprovements = "NeedImprovements", // Студент может доработать задачу сейчас или в будущем
	WrongAnswer = "WrongAnswer", // Тесты не пройдены или ошибка компиляции, показывается даже в старых версиях
	Message = "Message", // Сообщение, ни на что не влияющее, например, старая версия
}

interface ReviewInfoWithMarker extends ReviewInfoRedux {
	marker: TextMarker,
}

function getSubmissionColor(
	solutionRunStatus: SolutionRunStatus | undefined,
	checkingResult: CheckingResult | undefined, // undefined если automaticChecking null
	hasSuccessSolution: boolean, // Задача прошла автопроверку или автопроверки нет?
	selectedSubmissionIsLast: boolean, // Это последнее решение, прошедшее тесты?
	selectedSubmissionIsLastSuccess: boolean, // Это последнее решение, прошедшее тесты?
	prohibitFurtherManualChecking: boolean,
	isSkipped: boolean,
	isMaxScore: boolean, // Балл студента равен максимальному за задачу
): SubmissionColor {
	if(solutionRunStatus === SolutionRunStatus.CompilationError
		|| checkingResult === CheckingResult.CompilationError || checkingResult === CheckingResult.WrongAnswer || checkingResult == CheckingResult.RuntimeError) {
		return SubmissionColor.WrongAnswer;
	}
	if(solutionRunStatus === SolutionRunStatus.Ignored) {
		return SubmissionColor.NeedImprovements;
	}
	if(isSkipped) {
		return selectedSubmissionIsLast ? SubmissionColor.MaxResult : SubmissionColor.Message;
	}
	if(selectedSubmissionIsLastSuccess) {
		return !isMaxScore && !prohibitFurtherManualChecking
			? SubmissionColor.NeedImprovements
			: SubmissionColor.MaxResult;
	}
	return selectedSubmissionIsLast && !isMaxScore && !prohibitFurtherManualChecking
		? SubmissionColor.NeedImprovements
		: SubmissionColor.Message;
}

function IsSuccessSubmission(submission: SubmissionInfo | null): boolean {
	return !!submission && (submission.automaticChecking == null || submission.automaticChecking.result === CheckingResult.RightAnswer);
}

function hasSuccessSubmission(submissions: SubmissionInfo[]): boolean {
	return submissions.some(IsSuccessSubmission);
}

function submissionIsLast(submissions: SubmissionInfo[], submission: SubmissionInfo | null): boolean {
	return submissions.length > 0 && submissions[0] === submission;
}

function getLastSuccessSubmission(submissions: SubmissionInfo[]): SubmissionInfo | null {
	const successSubmissions = submissions.filter(IsSuccessSubmission);
	if(successSubmissions.length > 0) {
		return successSubmissions[0];
	}
	return null;
}

function isFirstRightAnswer(submissions: SubmissionInfo[], successSubmission: SubmissionInfo): boolean {
	const successSubmissions = submissions.filter(IsSuccessSubmission);
	return successSubmissions.length > 0 && successSubmissions[successSubmissions.length - 1] === successSubmission;
}

function isSubmissionShouldBeEditable(submission: SubmissionInfo): boolean {
	return submission.automaticChecking?.result !== AutomaticExerciseCheckingResult.RightAnswer && submission.automaticChecking?.result !== AutomaticExerciseCheckingResult.NotChecked;
}

function getReviewsWithoutDeleted(reviews: ReviewInfoWithMarker[]): ReviewInfoWithMarker[] {
	return reviews.map(r => ({ ...r, comments: r.comments.filter(c => !c.isDeleted && !c.isLoading) }));
}

function getAllReviewsFromSubmission(submission: SubmissionInfoRedux): ReviewInfoRedux[] {
	if(!submission) {
		return [];
	}

	const manual = submission.manualCheckingReviews || [];
	const auto = submission.automaticChecking && submission.automaticChecking.reviews ? submission.automaticChecking.reviews : [];
	return manual.concat(auto);
}

function createTextMarker(
	finishLine: number, finishPosition: number,
	startLine: number, startPosition: number,
	className: string,
	exerciseCodeDoc: Doc,
): TextMarker {
	return exerciseCodeDoc.markText({
		line: startLine,
		ch: startPosition
	}, {
		line: finishLine,
		ch: finishPosition
	}, {
		className,
	});
}

function getReviewsWithTextMarkers(
	submission: SubmissionInfoRedux,
	exerciseCodeDoc: Doc,
	markerClassName: string
): ReviewInfoWithMarker[] {
	const reviews = getAllReviewsFromSubmission(submission);

	const reviewsWithTextMarkers: ReviewInfoWithMarker[] = [];

	for (const review of reviews) {
		const { finishLine, finishPosition, startLine, startPosition } = review;
		const textMarker = createTextMarker(finishLine, finishPosition, startLine, startPosition,
			markerClassName,
			exerciseCodeDoc);

		reviewsWithTextMarkers.push({
			marker: textMarker,
			...review
		});
	}

	return reviewsWithTextMarkers;
}

function getSelectedReviewIdByCursor(
	reviews: ReviewInfoWithMarker[],
	exerciseCodeDoc: Doc,
	cursor: CodeMirror.Position
): number {
	const { line, ch } = cursor;
	const reviewsUnderCursor = reviews.filter(r =>
		r.startLine <= line && r.finishLine >= line
		&& !(r.startLine === line && ch < r.startPosition)
		&& !(r.finishLine === line && r.finishPosition < ch)
	);

	if(reviewsUnderCursor.length === 0) {
		return -1;
	}

	reviewsUnderCursor.sort((a, b) => {
		const aLength = getReviewSelectionLength(a, exerciseCodeDoc);
		const bLength = getReviewSelectionLength(b, exerciseCodeDoc);
		if(aLength !== bLength) {
			return aLength - bLength;
		}

		return a.startLine !== b.startLine
			? a.startLine - b.startLine
			: a.startPosition !== b.startPosition
				? a.startPosition - b.startPosition
				: new Date(a.addingTime ?? Math.random() * 10).getTime()
				- new Date(b.addingTime ?? Math.random() * 10).getTime();
	});

	return reviewsUnderCursor[0].id;
}

const getReviewSelectionLength = (review: ReviewInfoWithMarker, exerciseCodeDoc: Doc): number =>
	exerciseCodeDoc.indexFromPos({ line: review.finishLine, ch: review.finishPosition })
	- exerciseCodeDoc.indexFromPos({ line: review.startLine, ch: review.startPosition });


const loadLanguageStyles = (language: Language): string => {
	switch (language.toLowerCase()) {
		case Language.cSharp:
			require('codemirror/mode/clike/clike');
			return 'text/x-csharp';
		case Language.python2:
			require('codemirror/mode/python/python');
			return 'text/x-python';
		case Language.python3:
			require('codemirror/mode/python/python');
			return 'text/x-python';
		case Language.java:
			require('codemirror/mode/clike/clike');
			return 'text/x-java';
		case Language.javaScript:
			require('codemirror/mode/javascript/javascript');
			return 'text/javascript';
		case Language.html:
			require('codemirror/mode/xml/xml');
			return 'text/html';
		case Language.typeScript:
			require('codemirror/mode/javascript/javascript');
			return 'text/typescript';
		case Language.css:
			require('codemirror/mode/css/css');
			return 'text/css';
		case Language.haskell:
			require('codemirror/mode/haskell/haskell');
			return 'text/x-haskell';
		case Language.cpp:
			require('codemirror/mode/clike/clike');
			return 'text/x-c++src';
		case Language.c:
			require('codemirror/mode/clike/clike');
			return 'text/x-c';
		case Language.pgsql:
			require('codemirror/mode/sql/sql');
			return 'text/x-pgsql';
		case Language.mikrokosmos:
			return 'text/plain';

		case Language.text:
			return 'text/plain';

		case Language.jsx:
			require('codemirror/mode/jsx/jsx');
			return 'text/jsx';

		default:
			require('codemirror/mode/xml/xml');
			return 'text/html';
	}
};

function replaceReviewMarker(
	reviews: ReviewInfoWithMarker[],
	reviewId: number,
	newReviewId: number,
	doc: Doc,
	defaultMarkerClass: string,
	selectedMarkerClass: string,
): { reviews: ReviewInfoWithMarker[], selectedReviewLine: number, } {
	const newCurrentReviews = [...reviews];

	if(reviewId >= 0) {
		const review = newCurrentReviews.find(r => r.id === reviewId);
		if(review) {
			const { from, to, } = review.marker.find() as MarkerRange;
			review.marker.clear();
			review.marker =
				createTextMarker(to.line, to.ch, from.line, from.ch, defaultMarkerClass, doc);
		}
	}

	let line = 0;
	if(newReviewId >= 0) {
		const review = newCurrentReviews.find(r => r.id === newReviewId);
		if(review) {
			const { from, to, } = review.marker.find() as MarkerRange;
			review.marker.clear();
			review.marker =
				createTextMarker(to.line, to.ch, from.line, from.ch, selectedMarkerClass, doc);

			line = from.line;
		}
	}

	return { reviews: newCurrentReviews, selectedReviewLine: line, };
}

function isAcceptedSolutionsWillNotDiscardScore(submissions: SubmissionInfo[], isSkipped: boolean) {
	return submissions.filter(s => s.automaticChecking?.result === AutomaticExerciseCheckingResult.RightAnswer).length > 0 || isSkipped;
}

export {
	SubmissionColor,
	ReviewInfoWithMarker,

	getSubmissionColor,
	hasSuccessSubmission,
	submissionIsLast,
	getLastSuccessSubmission,
	isFirstRightAnswer,
	isSubmissionShouldBeEditable,
	getReviewsWithoutDeleted,
	getAllReviewsFromSubmission,
	createTextMarker,
	getReviewsWithTextMarkers,
	getSelectedReviewIdByCursor,
	loadLanguageStyles,
	replaceReviewMarker,
	isAcceptedSolutionsWillNotDiscardScore
};
