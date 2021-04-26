import React, { Component } from "react";
import { debounce } from "debounce";
import { CSSTransition, TransitionGroup } from "react-transition-group";

import { CommentLite } from "icons";
import { Toast } from "ui";
import Thread from "../Thread/Thread";
import CommentSendForm from "../CommentSendForm/CommentSendForm";
import Stub from "../Stub/Stub";
import Error404 from "../../common/Error/Error404";

import throttle from "src/utils/throttle";
import { UserInfo, } from "src/utils/courseRoles";

import { Comment, CommentPolicy } from "src/models/comments";
import { SlideType } from "src/models/slide";
import { CommentStatus } from "src/consts/comments";

import styles from "./CommentsList.less";
import {
	countAllComments,
	findIndexOfComment,
	getCommentsByCount, parseCommentIdFromHash,
} from "../utils";

const defaultPaginationOptions = {
	commentsPerPack: 15,
	scrollDistance: 500,
};

interface Props {
	key?: string;
	headerRef: React.RefObject<HTMLDivElement>;

	slideType: SlideType;

	courseId: string;
	slideId: string;
	user: UserInfo;
	isSlideReady: boolean;

	comments: Comment[];
	commentPolicy: CommentPolicy;

	handleTabChange: () => void;
	isSlideContainsComment: (commentId: number) => boolean;

	api: {
		addComment: (text: string, parentCommentId?: number) => Promise<Comment>;
		deleteComment: (commentId: number,) => Promise<void>;

		likeComment: (commentId: number) => Promise<unknown>;
		dislikeComment: (commentId: number) => Promise<unknown>;

		updateComment: (commentId: number,
			updatedFields?: Pick<Partial<Comment>, 'text' | 'isApproved' | 'isCorrectAnswer' | 'isPinnedToTop'>
		) => Promise<Comment>;
	}
}

interface State {
	status: string;
	saveCommentLikeStatus: null,
	commentEditing: CommentStatus;
	reply: CommentStatus;

	showFocus: boolean;
	sending: boolean;
	animation: boolean;

	commentsToRender: number;
}

export interface ActionsType {
	handleLikeClick: (commentId: number, isLiked: boolean) => void;
	handleCorrectAnswerMark: (commentId: number, isRightAnswer: boolean) => void;
	handleApprovedMark: (commentId: number, isApproved: boolean) => void;
	handlePinnedToTopMark: (commentId: number, isPinnedToTop: boolean) => void;
	handleEditComment: (commentId: number, text: string) => void;
	handleAddReplyComment: (parentCommentId: number, text: string) => void;
	handleDeleteComment: (commentId: number) => void;
	handleShowEditForm: (commentId: number | null) => void;
	handleShowReplyForm: (commentId: number | null) => void;
}

class CommentsList extends Component<Props, State> {
	private lastThreadRef: React.RefObject<HTMLDivElement> = React.createRef();
	private commentsListRef: React.RefObject<HTMLDivElement> = React.createRef();
	private commentsPaginationOptions: typeof defaultPaginationOptions = { ...defaultPaginationOptions };
	private throttleScroll: (() => void) | null = null;
	private additionalCommentsCountOnScroll = 5;

	private readonly debouncedSendData: null |
		((method: (commentId: number) => Promise<unknown>,
			commentId: number,
			updatedFields?: Pick<Partial<Comment>, 'text' | 'isApproved' | 'isCorrectAnswer' | 'isPinnedToTop'>
		) => void) = null;

	constructor(props: Props) {
		super(props);
		this.state = {
			saveCommentLikeStatus: null,
			commentEditing: {
				commentId: null,
				sending: false
			},
			reply: {
				commentId: null,
				sending: false
			},
			showFocus: false,
			sending: false,
			status: "",
			animation: false,
			commentsToRender: Math.min(this.commentsPaginationOptions.commentsPerPack, countAllComments(props.comments)),
		};

		this.debouncedSendData = debounce(this.sendData, 300);
	}

	componentDidMount(): void {
		this.throttleScroll = throttle(this.handleScrollToBottom, 100);
		window.addEventListener("scroll", this.throttleScroll);
		window.addEventListener("hashchange", this.handleScrollToCommentByHashFormUrl);

		this.handleScrollToCommentByHashFormUrl();
	}

	componentWillUnmount(): void {
		if(this.throttleScroll) {
			window.removeEventListener("scroll", this.throttleScroll);
		}
		window.removeEventListener("hashchange", this.handleScrollToCommentByHashFormUrl);
	}

	renderPackOfComments(packSize: number): void {
		const { commentsToRender, } = this.state;
		const { comments, } = this.props;

		this.setState({
			commentsToRender: Math.min(commentsToRender + packSize, countAllComments(comments)),
		});
	}

	setStateIfMounted(updater: Partial<State>, callback?: () => void): void {
		if(this.commentsListRef.current) {
			this.setState(updater as State, callback);
		}
	}

	handleScrollToBottom = (): void => {
		const { commentsToRender, } = this.state;
		const { comments, } = this.props;
		const { scrollDistance, commentsPerPack, } = this.commentsPaginationOptions;

		const element = document.documentElement;
		const windowRelativeBottom = element.getBoundingClientRect().bottom;
		if(windowRelativeBottom < (element.clientHeight + scrollDistance)
			&& commentsToRender < countAllComments(comments)) {
			this.renderPackOfComments(commentsPerPack);
		}
	};

	handleScrollToCommentByHashFormUrl = (): void => {
		const { comments, handleTabChange, isSlideContainsComment, } = this.props;
		const { commentsToRender, } = this.state;

		const hashId = parseCommentIdFromHash(window.location.hash);
		const indexOfComment = findIndexOfComment(hashId, comments);

		if(hashId >= 0) {
			if(indexOfComment >= commentsToRender) {
				const indexAsCount = indexOfComment + 1;
				this.renderPackOfComments(indexAsCount - commentsToRender + this.additionalCommentsCountOnScroll);
			} else if(indexOfComment < 0 && isSlideContainsComment(hashId)) {
				handleTabChange();
			}
		}
	};

	render(): React.ReactNode {
		const { status, commentsToRender, } = this.state;
		const { user, commentPolicy, key, courseId, slideId, } = this.props;

		if(status === "error") {
			return <Error404/>;
		}

		return (
			<div key={ key } ref={ this.commentsListRef }>
				{ !user.id &&
				<Stub hasThreads={ commentsToRender > 0 } courseId={ courseId } slideId={ slideId }/> }
				{ (user.id && commentPolicy && !commentPolicy.areCommentsEnabled) && this.renderMessageIfCommentsDisabled() }
				{ this.renderSendForm() }
				{ this.renderThreads() }
				{ (commentPolicy.areCommentsEnabled && user.id && commentsToRender > 7) &&
				<button className={ styles.sendButton } onClick={ this.handleShowSendForm }>
					<CommentLite color="#3072C4"/>
					Оставить комментарий
				</button> }
			</div>
		);
	}

	renderMessageIfCommentsDisabled(): React.ReactElement {
		return (
			<div className={ styles.textForDisabledComments }>
				В данный момент комментарии выключены.
			</div>
		);
	}

	renderSendForm(): React.ReactNode {
		const { sending, status, showFocus } = this.state;
		const { user, commentPolicy } = this.props;
		const focusedSendForm = { inSendForm: showFocus, };

		return (
			user.id && commentPolicy && (commentPolicy.areCommentsEnabled || commentPolicy.onlyInstructorsCanReply) &&
			<CommentSendForm
				isShowFocus={ focusedSendForm }
				author={ user }
				handleSubmit={ this.handleAddComment }
				sending={ sending }
				sendStatus={ status }/>
		);
	}

	renderThreads(): React.ReactElement {
		const { commentEditing, reply, animation, commentsToRender, } = this.state;
		const { user, slideType, courseId, commentPolicy, isSlideReady, slideId, comments, } = this.props;

		const transitionStyles = {
			enter: styles.enter,
			exit: styles.exit,
			enterActive: styles.enterActive,
			exitActive: styles.exitActive,
		};

		const duration = {
			enter: 1000,
			exit: 500,
		};

		const actions: ActionsType = {
			handleLikeClick: this.handleLikeClick,
			handleCorrectAnswerMark: this.handleCorrectAnswerMark,
			handleApprovedMark: this.handleApprovedMark,
			handlePinnedToTopMark: this.handlePinnedToTopMark,
			handleEditComment: this.handleEditComment,
			handleAddReplyComment: this.handleAddReplyComment,
			handleDeleteComment: this.handleDeleteComment,
			handleShowEditForm: this.handleShowEditForm,
			handleShowReplyForm: this.handleShowReplyForm,
		};

		return (
			<TransitionGroup enter={ animation }>
				{ getCommentsByCount(commentsToRender, comments)
					.map((comment, index, array) => {
						const ref = index === array.length - 1 ? this.lastThreadRef : null;

						return (
							<CSSTransition
								key={ comment.id }
								appear
								in={ animation }
								mountOnEnter
								unmountOnExit
								classNames={ transitionStyles }
								timeout={ duration }>
								<section className={ styles.thread } key={ comment.id } ref={ ref }>
									<Thread
										user={ user }
										slideType={ slideType }
										courseId={ courseId }
										slideId={ slideId }
										animation={ animation }
										comment={ comment }
										commentPolicy={ commentPolicy }
										commentEditing={ commentEditing }
										reply={ reply }
										actions={ actions }
										isSlideReady={ isSlideReady }/>
								</section>
							</CSSTransition>);
					}) }
			</TransitionGroup>
		);

	}

	updateComment = async (id: number, updatedFields: Partial<Comment>): Promise<void> => {
		const { api, comments, } = this.props;

		if(comments.some(c => c.id === id || c.replies.some(r => r.id === id))) {
			await api.updateComment(id, updatedFields);
		}
	};

	handleAddComment = async (text: string): Promise<void> => {
		const { api, } = this.props;
		const { commentsToRender, } = this.state;

		this.setState({
			sending: true,
			animation: true,
		});

		try {
			await api.addComment(text);
			this.setState({
				commentsToRender: commentsToRender + 1,
			});
		} catch (e) {
			Toast.push("Не удалось добавить комментарий. Попробуйте снова.");
			console.error(e);
		} finally {
			this.setState({
				sending: false,
			});
		}
	};

	handleLikeClick = async (commentId: number, isLiked: boolean): Promise<void> => {
		const { api, } = this.props;

		await this.debouncedSendData?.(isLiked ? api.dislikeComment : api.likeComment, commentId);
	};

	handleApprovedMark = (commentId: number, isApproved: boolean): void => {
		const { api, } = this.props;

		this.debouncedSendData?.(api.updateComment, commentId, { isApproved });
	};

	handleCorrectAnswerMark = (commentId: number, isCorrectAnswer: boolean): void => {
		const { api, } = this.props;

		this.debouncedSendData?.(api.updateComment, commentId, { isCorrectAnswer });
	};

	handlePinnedToTopMark = (commentId: number, isPinnedToTop: boolean): void => {
		const { api, } = this.props;

		this.debouncedSendData?.(api.updateComment, commentId, { isPinnedToTop });
	};

	handleShowSendForm = (): void => {
		this.handleScroll(this.props.headerRef);
		this.setState({
			showFocus: true,
		});
	};

	handleShowEditForm = (commentId: number | null): void => {
		this.setState({
			commentEditing: {
				...this.state.commentEditing,
				commentId: commentId,
			},
			showFocus: false,
		});
	};

	handleShowReplyForm = (commentId: number | null): void => {
		this.setState({
			reply: {
				...this.state.reply,
				commentId: commentId,
			},
			showFocus: false,
		});
	};

	handleEditComment = async (commentId: number, text: string): Promise<void> => {
		const { api, } = this.props;

		this.setState({
			commentEditing: {
				commentId: commentId,
				sending: true,
			},
			showFocus: false,
		});

		try {
			await api.updateComment(commentId, { text });

			this.setState({
				commentEditing: {
					commentId: null,
					sending: false,
				},
			});
		} catch (e) {
			Toast.push("Не удалось изменить комментарий. Попробуйте снова.");
			this.setState({
				commentEditing: {
					commentId: commentId,
				},
			});
			console.error(e);
		} finally {
			this.setState({
				commentEditing: {
					sending: false,
				},
			});
		}
	};

	handleAddReplyComment = async (parentCommentId: number, text: string): Promise<void> => {
		const { api, } = this.props;

		this.setState({
			animation: true,
			reply: {
				sending: true,
			}
		});

		try {
			await api.addComment(text, parentCommentId);

			this.setState({
				reply: {
					commentId: null,
				},
			});
		} catch (e) {
			Toast.push("Не удалось отправить комментарий. Попробуйте снова.");
			console.error(e);
		} finally {
			this.setState({
				reply: {
					sending: false,
				}
			});
		}
	};

	handleDeleteComment = async (commentId: number): Promise<void> => {
		const { api, } = this.props;

		this.setState({
			animation: true,
		});

		try {
			await api.deleteComment(commentId);

			Toast.push("Комментарий удалён", {
				label: "Восстановить",
				handler: () => {
					api.updateComment(commentId);
					Toast.push("Комментарий восстановлен");
				}
			});
		} catch (e) {
			Toast.push("Комментарий не удалён. Произошла ошибка, попробуйте снова");
			console.error(e);
		}
	};

	handleScroll = (ref: React.RefObject<HTMLDivElement>): void => {
		if(ref.current) {
			const rect = ref.current.getBoundingClientRect();
			window.scrollTo({
				left: 0,
				top: window.pageYOffset + rect.top,
				behavior: "auto"
			});
		}
	};

	sendData = (method: (commentId: number, property: unknown) => Promise<unknown>, commentId: number,
		property: unknown
	): Promise<unknown> =>
		method(commentId, property)
			.catch(e => {
				Toast.push("Не удалось изменить комментарий. Произошла ошибка, попробуйте снова");
				console.error(e);
			});
}

export default CommentsList;
