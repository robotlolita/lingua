module.exports = {
  mode: "none",
  entry: "./lingua.js",
  output: {
    filename: "app.js",
    libraryTarget: "commonjs"
  },
  externals: {
    "yargs": "commonjs yargs",
    "fs": "commonjs fs",
    "path": "commonjs path",
    "prettier": "commonjs prettier"
  }
}