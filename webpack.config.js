// This file is used to minify and tree-shake the bits of Snabbdom
// needed by Feliz.Snabbdom to include it in the Nuget package

const path = require("path");

module.exports = {
    entry: './src/Feliz.Snabbdom/snabbdom.js',
    mode: "production",
    output: {
        filename: "snabbdom.min.js",
        path: path.join(__dirname, "src/Feliz.Snabbdom"),
        libraryTarget: 'commonjs',
    },
  };