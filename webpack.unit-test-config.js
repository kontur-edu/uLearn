const path = require('path')
const nodeExternals = require('webpack-node-externals')
const glob = require('webpack-glob-entries')

module.exports = {
  mode: 'development',
  entry: glob(path.resolve(__dirname, 'unit_test', '*.js')),
  output: {
    path: path.resolve(__dirname, 'dist', 'unit_test'),
    filename: '[name].js',
  },
  target: 'node',
  node: {
    __dirname: false,
    __filename: false,
  },
  externals: [nodeExternals()],
  module: {
    rules: [
      {
        test: /\.js$/,
        exclude: /node_modules/,
        use: {
          loader: 'babel-loader',
          options: {
            presets: [
              ['@babel/preset-env', { targets: { node: 'current' } }],
              '@babel/preset-stage-0',
            ],
          },
        },
      },
    ],
  },
}
