import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom'
import { useEffect, useState } from 'react'
import LoginPage from './pages/LoginPage'
import AlertsPage from './pages/AlertsPage'
import CreateAlertPage from './pages/CreateAlertPage'
import AlertDetailPage from './pages/AlertDetailPage'
import ApprovalsPage from './pages/ApprovalsPage'
import DashboardPage from './pages/DashboardPage'
import './App.css'

// Protected route wrapper
interface ProtectedRouteProps {
  element: React.ReactNode
}

const ProtectedRoute = ({ element }: ProtectedRouteProps) => {
  const token = sessionStorage.getItem('authToken')
  return token ? element : <Navigate to="/login" replace />
}

function App() {
  const [isReady] = useState(true)

  useEffect(() => {
    // Check if user is already logged in
    const token = sessionStorage.getItem('authToken')
    if (!token && window.location.pathname !== '/login') {
      // Allow router to handle this
    }
  }, [])

  if (!isReady) {
    return <div>Loading...</div>
  }

  return (
    <Router>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/alerts" element={
          <ProtectedRoute element={<AlertsPage />} />
        } />
        <Route path="/alerts/create" element={
          <ProtectedRoute element={<CreateAlertPage />} />
        } />
        <Route path="/alerts/:alertId" element={
          <ProtectedRoute element={<AlertDetailPage />} />
        } />
        <Route path="/approvals" element={
          <ProtectedRoute element={<ApprovalsPage />} />
        } />
        <Route path="/dashboard" element={
          <ProtectedRoute element={<DashboardPage />} />
        } />
        <Route path="/" element={<Navigate to="/alerts" replace />} />
        <Route path="*" element={<Navigate to="/alerts" replace />} />
      </Routes>
    </Router>
  )
}

export default App
