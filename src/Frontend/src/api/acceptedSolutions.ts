﻿import { AcceptedSolutionsResponse, LikedAcceptedSolutionsResponse } from "../models/acceptedSolutions";
import api from "./index";
import { buildQuery } from "../utils";

export interface AcceptedSolutionsApi {
	getAcceptedSolutions: (courseId: string, slideId: string) => Promise<AcceptedSolutionsResponse>,
	getLikedAcceptedSolutions: (courseId: string, slideId: string, offset: number,
		count: number
	) => Promise<LikedAcceptedSolutionsResponse>,
	likeAcceptedSolution: (solutionId: number) => Promise<Response>,
	dislikeAcceptedSolution: (solutionId: number) => Promise<Response>,
	promoteAcceptedSolution: (courseId: string, solutionId: number) => Promise<Response>,
	unpromoteAcceptedSolution: (courseId: string, solutionId: number) => Promise<Response>
}

export function getAcceptedSolutions(courseId: string, slideId: string): Promise<AcceptedSolutionsResponse> {
	return api.get(`accepted-solutions?courseId=${ courseId }&slideId=${ slideId }`);
}

export function getLikedAcceptedSolutions(courseId: string, slideId: string, offset: number,
	count: number
): Promise<LikedAcceptedSolutionsResponse> {
	const query = buildQuery({ courseId, slideId, offset, count });
	return api.get(`accepted-solutions/liked` + query);
}

export function likeAcceptedSolution(solutionId: number): Promise<Response> {
	return api.put(`accepted-solutions/like?solutionId=${ solutionId }`);
}

export function dislikeAcceptedSolution(solutionId: number): Promise<Response> {
	return api.delete(`accepted-solutions/like?solutionId=${ solutionId }`);
}

export function promoteAcceptedSolution(courseId: string, solutionId: number): Promise<Response> {
	return api.put(`accepted-solutions/promote?courseId=${ courseId }&solutionId=${ solutionId }`);
}

export function unpromoteAcceptedSolution(courseId: string, solutionId: number): Promise<Response> {
	return api.delete(`accepted-solutions/promote?courseId=${ courseId }&solutionId=${ solutionId }`);
}