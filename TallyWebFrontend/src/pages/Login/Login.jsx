import { useState } from "react";
import { useNavigate } from "react-router-dom";
import "./Login.css";
import { loginUser } from "../../services/authService";

function Login() {
  const navigate = useNavigate();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");

  const handleLogin = async (e) => {
    e.preventDefault();

    try {
      const data = await loginUser(username, password);

      localStorage.setItem("token", data.token);
      localStorage.setItem(
        "user",
        JSON.stringify({
          id: data.id,
          username: data.username,
          fullName: data.fullName,
          role: data.role,
        }),
      );

      navigate("/dashboard");
    } catch (error) {
      alert(error.message);
    }
  };

  return (
    <div className="loginPage">
      <div className="loginCard">
        <div className="loginLogo" aria-label="Tally Web 2 logo">
          <svg viewBox="0 0 48 48" role="img" aria-hidden="true">
            <path d="M12 13h24v5H26v18h-5V18h-9z" fill="currentColor" />
            <path d="M29 25h7v11h-7z" fill="currentColor" opacity=".72" />
            <path d="M12 25h7v11h-7z" fill="currentColor" opacity=".48" />
          </svg>
        </div>

        <div className="loginBrand">
          <span className="loginEyebrow">Business Suite</span>
          <h1>Tally Web </h1>
          <p>Business Data Management</p>
        </div>

        <form onSubmit={handleLogin}>
          <label htmlFor="login-username">Username</label>
          <input
            id="login-username"
            type="text"
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder="Enter username"
          />

          <label htmlFor="login-password">Password</label>
          <input
            id="login-password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="Enter password"
          />

          <button type="submit" className="loginButton">
            <span>Sign In</span>
            <span className="loginButtonArrow" aria-hidden="true">
              →
            </span>
          </button>
        </form>

        <div className="loginFooter">
          Secure sign in to your business workspace
        </div>
      </div>
    </div>
  );
}

export default Login;
