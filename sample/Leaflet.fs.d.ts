type Model = {
    containerId: string,
    isPopupOpen: boolean,
    selectedMarker: string,
}

type Dispatch = {
    togglePopup(): void,
    markerSelected(name: string): void,
}