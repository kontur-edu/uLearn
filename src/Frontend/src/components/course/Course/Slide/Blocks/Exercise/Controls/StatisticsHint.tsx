import React from "react";
import classNames from "classnames";

import { Tooltip } from "ui";

import { AttemptsStatistics } from "src/models/exercise";

import styles from './Controls.less';

import texts from "../Exercise.texts";

export interface Props {
	attemptsStatistics: AttemptsStatistics
}

function StatisticsHint({ attemptsStatistics }: Props): React.ReactElement {
	const {
		attemptedUsersCount,
		usersWithRightAnswerCount,
		lastSuccessAttemptDate,
	} = attemptsStatistics;
	const statisticsClassName = classNames(styles.exerciseControls, styles.statistics);

	return (
		<span className={ statisticsClassName }>
			<Tooltip pos={ "bottom right" } trigger={ "hover&focus" } render={ renderTooltipContent }>
				{ texts.controls.statistics.buildShortText(usersWithRightAnswerCount) }
			</Tooltip>
		</span>
	);

	function renderTooltipContent() {
		return (
			<span>
				{ texts.controls.statistics.buildStatistics(attemptedUsersCount,
					usersWithRightAnswerCount, lastSuccessAttemptDate) }
			</span>);
	}
}

export default StatisticsHint;
