module Leaflet

open System
open Fable.Core
open Fable.Core.JsInterop
open Browser
open Browser.Types
open Feliz
open Feliz.Snabbdom
open type length

type Msg =
    | TogglePopup
    | MarkerSelected of name: string

type Model =
    { containerId: string
      isPopupOpen: bool
      selectedMarker: string }

let initMap(m: Model) (d: TsDispatcher<Msg>): (Model -> unit) = importMember "./leaflet.js"

let init() =
    { containerId = "map-container"
      isPopupOpen = true
      selectedMarker = "" }

let update msg model =
    match msg with
    | TogglePopup -> { model with isPopupOpen = not model.isPopupOpen }
    | MarkerSelected m -> { model with selectedMarker = m }

let view (model: Model) dispatch =
    Html.div [
        Html.div [
            Attr.id model.containerId
            Css.height 400
            Hook.subscribe(model, fun _vnode ->
                TsDeclaration.makeDispatcher model dispatch ||> initMap)
        ]

        if (not(String.IsNullOrEmpty(model.selectedMarker))) then
            Html.p $"Selected {model.selectedMarker}"

        Html.button [
            Attr.className "button"
            Ev.onClick (fun _ -> dispatch TogglePopup)
            Html.text $"""{if model.isPopupOpen then "Close" else "Open"} popup"""
        ]
    ]

let mkProgram() =
    Elmish.Program.mkSimple init update view
