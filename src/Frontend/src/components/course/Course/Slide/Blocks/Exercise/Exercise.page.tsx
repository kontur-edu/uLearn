import { connect } from "react-redux";
import { Dispatch } from "redux";

import Exercise from './Exercise';

import { AutomaticExerciseCheckingResult as CheckingResult } from "src/models/exercise";
import { RootState } from "src/models/reduxState";

import { sendCode, addReviewComment, deleteReviewComment, } from "src/actions/exercise";
import { skipExercise } from "src/actions/userProgress";

import { Language } from "src/consts/languages";
import { MatchParams } from "src/models/router";

const mapStateToProps = (state: RootState, { courseId, slideId, }: MatchParams) => {
	const { slides, account, userProgress, device, } = state;
	const { submissionsByCourses, submissionError, lastCheckingResponse, } = slides;
	const slideProgress = userProgress?.progress[courseId]?.[slideId] || {};

	const submissions = Object.values(submissionsByCourses[courseId][slideId])
		.filter((s, i, arr) =>
			(i === arr.length - 1)
			|| (!s.automaticChecking || s.automaticChecking.result === CheckingResult.RightAnswer));

	//newer is first
	submissions.sort((s1, s2) => (new Date(s2.timestamp).getTime() - new Date(s1.timestamp).getTime()));

	return {
		isAuthenticated: account.isAuthenticated,
		submissions,
		submissionError,
		lastCheckingResponse: !(lastCheckingResponse && lastCheckingResponse.courseId === courseId && lastCheckingResponse.slideId === slideId) ? null : lastCheckingResponse,
		user: account,
		slideProgress,
		deviceType: device.deviceType,
	};
};

const mapDispatchToProps = (dispatch: Dispatch) => ({
	sendCode: (courseId: string, slideId: string, code: string, language: Language,
	) => sendCode(courseId, slideId, code, language)(dispatch),

	addReviewComment: (courseId: string, slideId: string, submissionId: number, reviewId: number,
		comment: string
	) => addReviewComment(courseId, slideId, submissionId, reviewId, comment)(dispatch),

	deleteReviewComment: (courseId: string, slideId: string, submissionId: number, reviewId: number,
		commentId: number
	) => deleteReviewComment(courseId, slideId, submissionId, reviewId, commentId)(dispatch),

	skipExercise: (courseId: string, slideId: string, onSuccess: () => void,
	) => skipExercise(courseId, slideId, onSuccess)(dispatch),
});

export default connect(mapStateToProps, mapDispatchToProps)(Exercise);
