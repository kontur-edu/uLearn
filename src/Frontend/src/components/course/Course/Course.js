import React, { Component } from "react";
import PropTypes from "prop-types";
import UnitNavigation from "../Navigation/Unit/UnitNavigation";
import CourseNavigation from "../Navigation/Course/CourseNavigation";
import AnyPage from '../../../pages/AnyPage'
import { constructPathToSlide } from '../../../consts/routes';
import styles from "./Course.less"

class Course extends Component {
	constructor(props) {
		super(props);

		this.state = {
			onCourseNavigation: true,
			openUnit: null,
			highlightedUnit: Course.findActiveUnit(props.slideId, props.courseInfo),
		};
	}

	componentDidMount () {
		const { loadCourse, loadUserProgress, isAuthenticated, courseId, courseInfo, progress } = this.props;

		if (!courseInfo) {
			loadCourse(courseId);
		}

		if (isAuthenticated && !progress) {
			loadUserProgress(courseId);
		}
	}

	static getDerivedStateFromProps(props, state) {
		if (!state.highlightedUnit && props.courseInfo) {
			return {
				highlightedUnit: Course.findActiveUnit(props.slideId, props.courseInfo)
			};
		}

		return null;
	}

	render () {
		const { courseInfo } = this.props;
		const { onCourseNavigation } = this.state;

		if (!courseInfo) {
			return null;
		}


		return (
			<div className={ styles.root }>
				{ onCourseNavigation ? this.renderCourseNavigation() : this.renderUnitNavigation()}
				<div>
					<AnyPage />
				</div>
			</div>
		);
	}

	renderCourseNavigation () {
		const { courseInfo } = this.props;
		const { highlightedUnit } = this.state;

		return (
			<CourseNavigation
				title={ courseInfo.title }
				description={ courseInfo.description }
				progress={ 0.4 } // TODO: считать реальный
				items={courseInfo.units.map(item => ({
					title: item.title,
					id: item.id,
					isActive: highlightedUnit === item.id,
					onClick: this.unitClickHandle,
				}))}
			/>
		);
	}

	renderUnitNavigation () {
		const { openUnit } = this.state;
		const { courseInfo, courseId, slideId } = this.props;

		return (
			<UnitNavigation
				title={ openUnit.title }
				courseName={ courseInfo.title}
				onCourseClick={ this.returnInUnitsMenu }
				items={ openUnit.slides.map(item => ({
					id: item.id,
					title: item.title,
					type: item.type,
					url: constructPathToSlide(courseId, item.slug),
					isActive: item.slug === slideId,
					maxScore: item.maxScore,
					score: 0, // TODO: настоящий счет
				})) }
			/>
		);
	}

	static findActiveUnit(slideId, courseInfo) {
		if (!courseInfo || !courseInfo.units) {
			return null;
		}

		const units = courseInfo.units;


		for (const unit of units) {
			for (const slide of unit.slides) {
				if (slideId === slide.slug) {
					return unit.id;
				}
			}
		}

		return  null;
	}

	unitClickHandle = (id) => {
		const { units } = this.props;

		this.setState({
			openUnit: units[id],
			onCourseNavigation: false,
		});
	};

	returnInUnitsMenu = () => {
		this.setState({
			openUnit: null,
			onCourseNavigation: true,
		});
	};

}

Course.propTypes = {
	isAuthenticated: PropTypes.bool,
	courseId: PropTypes.string,
	slideId: PropTypes.string,
	courseInfo: PropTypes.object, // TODO: описать
	progress: PropTypes.object, // TODO: описать
	units: PropTypes.object,
	loadCourse: PropTypes.func,
	loadUserProgress: PropTypes.func,
};

export default Course;