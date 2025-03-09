import React from 'react';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import InfoPage from './components/InfoPage';
import Toolbar from './components/Toolbar';

/**
 * App component defines the main routes and navigation.
 * It logs application mounting.
 */
function App() {
  console.log("[App] Application mounted.");
  return (
    <Router>
      <nav>
        <ul>
          <li>
            <Link to="/info">Info Page</Link>
          </li>
          <li>
            <Link to="/toolbar">Toolbar</Link>
          </li>
        </ul>
      </nav>
      <Routes>
        <Route path="/info" element={<InfoPage />} />
        <Route path="/toolbar" element={<Toolbar />} />
      </Routes>
    </Router>
  );
}

export default App;
