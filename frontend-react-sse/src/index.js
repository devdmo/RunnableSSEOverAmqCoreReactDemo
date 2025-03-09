import React from 'react';
import ReactDOM from 'react-dom';
import App from './App';

/**
 * Renders the React application into the root DOM element.
 * Logs the initial render event.
 */
ReactDOM.render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
  document.getElementById('root')
);
