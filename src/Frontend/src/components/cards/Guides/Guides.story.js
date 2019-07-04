import React from 'react';
import {storiesOf} from '@storybook/react';
import Guides from './Guides';

storiesOf('Cards/UnitPage/Guides', module)
	.add('standard guides', () => (
		<Guides guides={["Подумайте над вопросом, перед тем как смотреть ответ.",
			"Оцените, на сколько хорошо вы знали ответ. Карточки, которые вы знаете плохо, будут показываться чаще",
			"Регулярно пересматривайте карточки, даже если вы уверенны в своих знаниях. Чем чаще использовать карточки, тем лучше они запоминаются."]}/>
	))
	.add('fish long text', () => (
		<Guides
			guides={[getLongText(), "short"]}/>
	));

const getLongText = () => {
	return "Идейные соображения высшего порядка, а также рамки и место" +
		" обучения кадров обеспечивает широкому кругу (специалистов) участие в" +
		" формировании соответствующий условий активизации. Разнообразный и богатый " +
		"опыт начало повседневной работы по формированию позиции требуют определения " +
		"и уточнения позиций, занимаемых участниками в отношении поставленных задач.\n" +
		"Повседневная практика показывает, что новая модель организационной деятельности" +
		" требуют от нас анализа систем массового участия. Товарищи! консультация с широким" +
		" активом представляет собой интересный эксперимент проверки позиций, занимаемых" +
		" участниками в отношении поставленных задач. Таким образом консультация с широким " +
		"активом позволяет оценить значение системы обучения кадров, соответствует насущным потребностям. " +
		"Идейные соображения высшего порядка, а также постоянный количественный рост и сфера нашей" +
		" активности способствует подготовки и реализации систем массового участия. Разнообразный и " +
		"богатый опыт постоянное информационно-пропагандистское обеспечение нашей деятельности способствует" +
		" подготовки и реализации направлений прогрессивного развития. Идейные соображения высшего" +
		" порядка, а также реализация намеченных плановых заданий в значительной степени обуславливает " +
		"создание направлений прогрессивного развития.";
};