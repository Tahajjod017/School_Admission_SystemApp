import { useEffect, useState } from "react";

const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:8080";

type Health = { status: string; database: string; holdDurationSeconds: number };
type State =
  | { kind: "loading" }
  | { kind: "ok"; health: Health }
  | { kind: "error" };

export default function App() {
  const [state, setState] = useState<State>({ kind: "loading" });

  useEffect(() => {
    fetch(`${API_URL}/health`)
      .then((r) => (r.ok ? (r.json() as Promise<Health>) : Promise.reject(new Error("bad status"))))
      .then((health) => setState({ kind: "ok", health }))
      .catch(() => setState({ kind: "error" }));
  }, []);

  return (
    <div className="card">
      <h1>School Admission</h1>
      <p>Milestone 0: checking that the frontend can reach the backend.</p>
      {state.kind === "loading" && <p>Checking server…</p>}
      {state.kind === "ok" && (
        <p className="ok">
          Server is running. Database: {state.health.database}. Seats are held for{" "}
          {state.health.holdDurationSeconds} seconds.
        </p>
      )}
      {state.kind === "error" && (
        <p className="bad">Cannot reach the server. Please make sure it is running and try again.</p>
      )}
    </div>
  );
}
