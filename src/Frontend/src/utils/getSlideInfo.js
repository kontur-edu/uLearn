import { acceptedAlert, acceptedSolutions, coursePath, ltiSlide } from "src/consts/routes";

import queryString from "query-string";

export default function getSlideInfo(location,) {
	const { pathname, search } = location;
	const isInsideCourse = pathname
		.toLowerCase()
		.startsWith(`/${ coursePath }`);
	if(!isInsideCourse) {
		return null;
	}

	const params = queryString.parse(search);
	const slideIdInQuery = params.slideId;
	const slideSlugOrAction = isInsideCourse ? pathname.split('/').slice(-1)[0] : undefined;

	let slideId;
	let isLti = false;
	let isAcceptedAlert = false;
	if(slideIdInQuery) {
		const action = slideSlugOrAction;
		slideId = slideIdInQuery;
		isAcceptedAlert = action.toLowerCase() === acceptedAlert;
		isLti = action.toLowerCase() === ltiSlide || isAcceptedAlert || params.isLti;
	} else {
		const slideSlug = slideSlugOrAction;
		slideId = slideSlug.split('_').pop();
	}

	const isReview = params.CheckQueueItemId !== undefined;
	const isAcceptedSolutions = slideSlugOrAction.toLowerCase() === acceptedSolutions;

	return {
		slideId,
		isReview,
		isLti,
		isAcceptedAlert,
		isAcceptedSolutions,
	}
}
