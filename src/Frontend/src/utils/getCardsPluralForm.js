import getPluralForm from "./getPluralForm";

export default function getCardsPluralForm(cardsCount = 0) {
	return (
		`${cardsCount} ${getPluralForm(cardsCount, 'карточка', 'карточки', 'карточек')}`
	);
}
