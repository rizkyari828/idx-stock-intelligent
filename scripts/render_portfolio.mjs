// Render the actual holdings UI against the acceptance API response, without a browser dependency.
import { createRequire } from 'node:module';
import { PortfolioTable } from '../frontend/.test-output/PortfolioTable.js';
import { coverageLabel } from '../frontend/.test-output/view.js';
const require = createRequire(new URL('../frontend/package.json', import.meta.url));
const { createElement } = require('react');
const { renderToStaticMarkup } = require('react-dom/server');
let input = '';
for await (const chunk of process.stdin) input += chunk;
const view = JSON.parse(input);
process.stdout.write(coverageLabel(view) + renderToStaticMarkup(createElement(PortfolioTable, { holdings: view.holdings, onOpen: () => {} })));
