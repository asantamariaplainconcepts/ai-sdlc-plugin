import { useCallback, useEffect, useState } from "react";

/**
 * One screen: the header of a change and a step rail placeholder.
 *
 * The header states the eight facts of the worktree it reads, each absence named with its
 * remedy: null is not zero, "could not be asked" is not "there is none", and no fact is ever
 * a tick. The rail below is POC-01's — drawn here as a placeholder that says so, rather than
 * disappearing or pretending to be empty.
 */

type Fact = {
  key: string;
  text: string;
  tone: "plain" | "warn" | "bad";
  title?: string | null;
};

type Header = {
  path: string;
  branch: string | null;
  notARepository: string | null;
  facts: Fact[];
};

const ink = {
  plain: "var(--muted-foreground)",
  warn: "var(--warn)",
  bad: "var(--bad)",
} as const;

const steps = [
  { id: "proposal", label: "Proposal" },
  { id: "code", label: "Code" },
  { id: "tests", label: "Tests" },
] as const;

export function App() {
  const [path, setPath] = useState(new URLSearchParams(window.location.search).get("path") ?? ".");
  const [reading, setReading] = useState<Header | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [step, setStep] = useState<string>("proposal");

  const read = useCallback(async (target: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`/api/header?path=${encodeURIComponent(target)}`);
      if (!response.ok) {
        throw new Error(`the server said ${response.status}`);
      }
      const body = (await response.json()) as Header;
      setReading(body);
    } catch (e) {
      setError(e instanceof Error ? e.message : "the header could not be read");
      setReading(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void read(path);
  }, [path, read]);

  return (
    <div style={{ minHeight: "100%", display: "flex", flexDirection: "column", gap: 10, padding: 14, maxWidth: 1080, margin: "0 auto" }}>
      <div style={{ flex: "none", display: "flex", gap: 8, alignItems: "center" }}>
        <input
          value={path}
          onChange={(e) => setPath(e.target.value)}
          spellCheck={false}
          placeholder="path to a worktree"
          className="mono"
          style={{ flex: 1, minWidth: 0, padding: "6px 10px", borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", color: "var(--foreground)", fontSize: 13 }}
        />
        <button
          type="button"
          onClick={() => void read(path)}
          disabled={loading}
          style={{ padding: "6px 12px", borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", color: "var(--foreground)", cursor: "pointer" }}
        >
          {loading ? "reading…" : "read"}
        </button>
      </div>

      {error ? (
        <div style={{ padding: 14, borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", color: "var(--bad)", fontSize: 13 }}>
          the header could not be read: {error}
        </div>
      ) : null}

      {/* The header: which working copy, which branch, then the facts as one line of phrases. */}
      <div style={{ flex: "none", padding: "12px 14px", borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", display: "flex", flexDirection: "column", gap: 8 }}>
        <div style={{ display: "flex", alignItems: "baseline", gap: 8, flexWrap: "wrap" }}>
          <span style={{ fontSize: 14, fontWeight: 600 }}>
            {reading ? reading.branch ?? "no branch" : "reading…"}
          </span>
          <span className="mono" style={{ fontSize: 12, color: "var(--muted-foreground)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {reading?.path}
          </span>
        </div>

        {reading?.notARepository ? (
          <span style={{ fontSize: 13, color: "var(--muted-foreground)" }}>{reading.notARepository}</span>
        ) : (
          <div className="mono" aria-label="What is true of this change" style={{ display: "flex", flexWrap: "wrap", alignItems: "baseline", columnGap: 6, rowGap: 2, fontSize: 12 }}>
            {(reading?.facts ?? []).map((fact, index) => (
              <span key={fact.key} style={{ display: "flex", alignItems: "baseline", gap: 6 }}>
                {index === 0 ? null : <span aria-hidden>·</span>}
                <span style={{ color: ink[fact.tone] }} title={fact.title ?? undefined}>{fact.text}</span>
              </span>
            ))}
          </div>
        )}
      </div>

      {/* The step rail: a placeholder that names what it is waiting for (POC-01 makes these
          data from .harness/review.json) rather than vanishing or faking emptiness. */}
      <div role="group" aria-label="The steps of a review" style={{ flex: "none", display: "flex", flexWrap: "wrap", gap: 8 }}>
        {steps.map((entry, index) => (
          <button
            key={entry.id}
            type="button"
            aria-pressed={entry.id === step}
            disabled
            title="steps arrive with POC-01, read from .harness/review.json"
            onClick={() => setStep(entry.id)}
            style={{ flex: "1 1 180px", minWidth: 0, display: "flex", alignItems: "center", gap: 8, padding: "8px 10px", textAlign: "left", border: `1px solid ${entry.id === step ? "var(--primary)" : "var(--border)"}`, borderRadius: "var(--radius)", background: "var(--card)", color: "var(--muted-foreground)", cursor: "default", fontSize: 13, fontWeight: 600 }}
          >
            <span aria-hidden style={{ width: 22, height: 22, display: "flex", alignItems: "center", justifyContent: "center", borderRadius: "50%", background: "var(--muted)", fontSize: 11 }}>{index}</span>
            {entry.label}
          </button>
        ))}
      </div>

      {/* The panel: one step at a time. Each later step is one panel component file, mounted here
          in exactly one line. */}
      <div style={{ flex: 1, minHeight: 120, padding: 14, borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", fontSize: 13, color: "var(--muted-foreground)" }}>
        The steps are not implemented yet — they arrive as declared data in POC-01, and each panel
        lands as its own component file mounted in one line above.
      </div>
    </div>
  );
}
