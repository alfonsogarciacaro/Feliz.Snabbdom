# Feliz.Snabbdom

Use [Snabbdom](https://github.com/snabbdom/snabbdom) in Fable/Elmish apps with a Feliz-like API (see [differences with Feliz](https://github.com/alfonsogarciacaro/Feliz.Engine)), as an alternative Virtual DOM implementation.

## Features

### Lightweight

Feliz.Snabbdom Nuget package already includes the JS source for Snabbdom so you don't need to add any extra npm dependency to your project. The bundle size for a "Hello World" Elmish.Snabbdom app **is only 16KB** when minified and gzipped.

### No DOM Abstractions

Bug or feature, depending on how you see it. Snabbdom doesn't provides abstractions over the DOM: attributes and events are attached directly to the HTML elements. Unlike React, there are no controlled form elements, `value` can only be set once and `onchange` is only triggered when an input loses focus (you can still use `oninput`).

### Just Functions

Same as there are no DOM abstractions, Snabbdom doesn't include any other concept like React components besides HTML/SVG elements (or nodes). You can just use the usual F# mechanisms for function composition to build your views.

> `Elmish.Snabbdom` does provide a way to embed an Elmish app within another as if it were a "component", see below.

### Lifecycle hooks

Snabbdom provides [hooks](https://github.com/snabbdom/snabbdom/tree/d66905438dc6866b2a7ab21d719c45a156d1252e#hooks) to different points of the nodes' lifecycle, the most important being `insert` and `destroy`. These can be used to run side effects when the node is inserted or removed from the DOM, and they're particularly useful to access the underlying HTML element as the virtual node with a reference to it is passed as argument. Feliz.Snabbdom provides a couple of useful overloads, for example if `Hook.insert` returns a disposable, this will be automatically run when the node is destroyed. For example:

```fsharp
// When the input element appears, I want to select all the text and attach an event to
// the document body to detect clicks outside the containing box and cancel the edit

Html.input [
    Attr.classes [ "input"; "is-medium" ]
    Attr.value editing
    Ev.onTextChange (SetEditedDescription >> dispatch)
    onEnterOrEscape dispatch ApplyEdit CancelEdit

    Hook.insert(fun vnode ->
        let el = vnode.elm.AsInputEl
        el.select() // Select all text

        // BodyEv is a helper using Feliz.Engine.Event to attach
        // events to document.body and return a disposable to detach them
        let parentBox = findParentWithClass "box" el
        BodyEv.onMouseDown(fun ev ->
            if not (parentBox.contains(ev.target :?> _)) then
                CancelEdit |> dispatch)
    )
]
```

### Interaction with JS libraries

The Virtual DOM and Elmish are a highly productive combo to write web apps in a declarative manner taking advantage of functional programming, but sometimes we need to interact with an "imperative" JS library like Leaflet, Monaco, D3 or p2.js. Lifecycle hooks are perfect in this case. Feliz.Snabbdom provides `Hook.subscribe: arg: 'arg * onInsert: VNode -> IObserver<'arg>` which is a combination of the insert/update/destroy hooks and lets you:

- Initialize the JS library when the node is inserted into the DOM
- Get notified (through the observer's `OnNext` method) with a new arg when a new render of the node is requested
- Get notified (through the observer's `OnCompleted` method) when the node is removed from the DOM

When initializing this kind of imperative components within Elmish, you can use the [Fable.TsDeclaration](https://github.com/alfonsogarciacaro/Fable.TsDeclaration) plugin for better interop between F# and JS (or Typescript). For example, if you want to render a map using [Leaflet](https://leafletjs.com/):

```fsharp
// Import an initialization function from JS code, here it will return
// another function to notify changes in the Elmish model
let initMap (m: Model) (d: TsDispatcher<Msg>): (Model -> unit) =
    importMember "./leaflet.js"

let view (model: Model) dispatch =
    Html.div [
        Attr.id model.containerId
        Css.height 400
        // This Hook.subscribe overload will create an IObserver that only
        // reacts to `OnNext` (`OnCompleted` is ignored). TsDeclaration.makeDispatcher
        // will create a JS object from a dispatch function to generate the messages,
        // and also generate a .d.ts file that we can use from JS/TS
        Hook.subscribe(model, fun _vnode ->
            TsDeclaration.makeDispatcher model dispatch ||> initMap)
    ]
```

Then, in the JS file we can do (code adapted from Leaflet guide):

```js
// @ts-check

import L from "leaflet";
import "leaflet/dist/leaflet.css";

/**
 * @param {L.Marker} marker
 * @param {boolean} isOpen
 */
function handlePopup(marker, isOpen) {
    if (isOpen) {
        marker.openPopup();
    } else {
        marker.closePopup();
    }
}


/**
 * @param {Model} model
 * @param {Dispatch} dispatch
 * @returns {(x: Model) => void}
 */
export function initMap(model, dispatch) {
    const map = L.map(model.containerId).setView([51.505, -0.09], 13);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);

    const marker = L.marker([51.5, -0.09]).addTo(map)
        .bindPopup('A pretty CSS3 popup.<br> Easily customizable.');

    marker.on("click", () => dispatch.markerSelected("default"))
    handlePopup(marker, model.isPopupOpen);

    return (x) => handlePopup(marker, x.isPopupOpen);
}
```

See how it's done [in the example](https://github.com/alfonsogarciacaro/Feliz.Snabbdom/blob/f7c64e02fc28797822fb2aedaa557f1dcfdab0dc/sample/Leaflet.fs).

### Memoization

In Elmish apps you run the full `view` function whenever the model changes. You may want to skip re-rendering the parts that don't need to change to avoid unnecessary calculations. For that you just need to wrap the function call with `memoize`. Example:

```fsharp
open Feliz.Snabbdom

// Instead of:
Htm.ul (model.Items |> List.map (renderItem dispatch))

// Do this (the Item type must include an Id: Guid property)
Htm.ul (model.Items |> List.map (memoize (renderItem dispatch)))
```

> See how it's done [in the sample](https://github.com/alfonsogarciacaro/Feliz.Snabbdom/blob/f7c64e02fc28797822fb2aedaa557f1dcfdab0dc/sample/Todos.fs#L301).

### CSS Transitions

Snabbdom supports [CSS transitions](https://github.com/snabbdom/snabbdom/tree/d66905438dc6866b2a7ab21d719c45a156d1252e#delayed-properties) on any element. In F# you can use them as follows ([example](https://github.com/alfonsogarciacaro/Feliz.Snabbdom/blob/f7c64e02fc28797822fb2aedaa557f1dcfdab0dc/sample/Todos.fs#L270-L282)):

```fsharp
Html.li [
    // Initial values
    Css.opacity 0.
    Css.transformScale 1.5

    // Transition properties ("all" cannot be used) and duration
    Css.transitionProperty (transitionProperty.opacity, transitionProperty.transform)
    Css.transitionDurationSeconds 0.5

    // Set the actual values after the element is inserted
    Css.delayed [
        Css.opacity 1.
        Css.transformScale 1.
    ]

    // Change the values when the element is removed
    Css.remove [
        Css.opacity 0.
        Css.transformScale 0.1
    ]
]
```

### Hot Module Replacement

The Elmish.Snabbdom package provides support for [HMR](https://elmish.github.io/hmr/) so the state of your app will be preserved when hot reloading. Just open the `Elmish.Snabbdom.HMR` module and you're good to go. See the "Getting Started" section below.

### Loosely Coupled Components

When trying to "componentize" Elmish apps, there are many different parts you need to wire (the init, update, dispatch and view functions, and the Msg and Model types) which can be a bit cumbersome sometimes (but gives you type safety). Elmish.Snabbdom provides a way to embed an Elmish app into another and just communicate through the arguments passed during renders, as with React components and props. You can also communicate from the child to the parent app (if necessary) by the usual Elmish mechanism: passing a `dispatch` function. In your child app:

```fsharp
open Elmish
open Elmish.Snabbdom
open Feliz.Snabbdom

// If necessary, declare a type for the messages sent to the parent
type ExtMsg = ..

// The update function will optionally return a message for the parent
let update (msg: Msg) (model: Model): Model * Cmd<Msg> * (ExtMsg option) = ..

// mkProgram accepts a dispatch function to send the external messages
let mkProgram (dispatch: ExtMsg -> unit): Program<Arg, Model, Msg, Node> =
  let update msg model =
    let model, cmd, extMsg = update msg model
    extMsg |> Option.iter dispatch
    model, cmd

  Program.mkProgram init update view
  // When the virtual node is re-rendered by the parent,
  // convert the new argument into a message and call `update`
  |> Program.withSetNewArg (fun (arg: Arg) -> NewArg arg)
```

Then you can easily mount the child app in virtual node of the parent view:

```fsharp
let view model dispatch =
    let childArg = ..

    Html.div [
        Attr.className "container"

        // Pass a selector like `tag[#id][.classes]` for the virtual node that will be created
        Child.mkProgram (fun msg -> ChildMsg msg |> dispatch)
        |> Program.mountOnVNodeWith "div.child" childArg
    ]
```

> See how it's done [in the example](https://github.com/alfonsogarciacaro/Feliz.Snabbdom/blob/f7c64e02fc28797822fb2aedaa557f1dcfdab0dc/sample/Todos.fs#L288-L289), but note this is just done for illustration purposes. In a real app you'd probably just put the Timer functionality in the Todos app.

### Lazily Loaded Components

Embedding Elmish apps can be even more useful if we can make them work with the [code splitting](https://webpack.js.org/guides/code-splitting/#dynamic-imports) feature of bundlers like Webpack. Similar to [React.lazy](https://reactjs.org/docs/code-splitting.html#reactlazy) you can use `Program.lazyOnVNode` and pass a reference to the `mkProgram` function in the component that Fable will convert into a dynamic JS `import()`. When Webpack finds this, it will separate the code from the main bundle so it's not loaded until it's really needed (helping your app start up faster).

```fsharp
Html.div [
    Attr.className "container"

    Html.div [
        Attr.className "tabs"
        Html.ul [
            tab model dispatch Todos
            tab model dispatch Dragging
            tab model dispatch Leaflet
        ]
    ]

    // The code for each module won't be loaded until the appropriate tab is selected
    // Make sure to use a different selector so the node is updated when the tab changes
    match model.CurrentTab with
    | Todos -> Program.lazyOnVNode Todos.mkProgram "div.todos"
    | Dragging -> Program.lazyOnVNode Dragging.mkProgram "div.dragging"
    | Leaflet -> Program.lazyOnVNode Leaflet.mkProgram "div.leaflet"
]
```

> ATTENTION: This feature requires fable `3.1.7` or higher.

> Make sure you don't reference anything else in the component module so the code doesn't end up in the generated bundle. Take special care when passing arguments, for example, don't construct a record declared in the component module. Put the type in a shared file or use an anonymous record instead.

See how it's done [in the example](https://github.com/alfonsogarciacaro/Feliz.Snabbdom/blob/f7c64e02fc28797822fb2aedaa557f1dcfdab0dc/sample/App.fs#L54-L58), but don't go too crazy with lazy components, they can complicate development (e.g. HMR doesn't work yet with embedded apps) and the benefit won't be noticeable unless the external module is quite big. Most of the time it will only be important when the module is loading a big JS dependency, and in those cases you can also use `Fable.Core.JsInterop.importDynamic` with hooks. For example, you can extend `Hook.subscribe` to accept an asynchronous response:

```fsharp
type Hook with
    static member subscribe(arg: 'arg, onInsert: Snabbdom.VNode -> JS.Promise<'arg->unit>) =
        Hook.subscribe(arg, fun vnode ->
            let onNext: ('arg -> unit) option ref = ref None

            onInsert vnode |> Promise.iter (fun o -> onNext := Some o)

            fun v -> onNext.contents |> Option.iter (fun f -> f v)
        )
```

And use it like this:

```fsharp
Hook.subscribe(model, fun _vnode ->
    importDynamic "./leaflet.js"
    |> Promise.map (fun jsModule ->
        let model, dispatcher = TsDeclaration.makeDispatcher model dispatch
        jsModule?initMap(model, dispatcher))
)
```


## Getting started

In most cases you'll want to use Elmish to structure your app. So you can install the `Elmish.Snabbdom` package directly (published as beta at the time of writing):

```bash
dotnet add package Elmish.Snabbdom --prerelease

# Or, if you use Paket:
dotnet paket add Elmish.Snabbdom
```

You can read more about [Elmish](https://elmish.github.io/), [Feliz.Engine](https://github.com/alfonsogarciacaro/Feliz.Engine) and [Snabbdom](https://github.com/snabbdom/snabbdom). Also check the `sample` directory in this repository for a sample app including all the features listed above. The basic skeleton will be:

```fsharp
open Feliz
open Feliz.Snabbdom
open Elmish
open Elmish.Snabbdom

// This activates HMR in debug mode
open Elmish.Snabbdom.HMR

let init() = ..
let update msg model = ..
let view model dispatch = ..

Program.mkSimple init update view
|> Program.mountWithId "app-container" // Alias: Program.withSnabbdom
|> Program.run
```