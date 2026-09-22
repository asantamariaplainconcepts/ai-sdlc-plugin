import { Check } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { CodeStep } from "./steps/CodeStep";
import { ProposalStep } from "./steps/ProposalStep";
import { TestsStep } from "./steps/TestsStep";

/**
 * One screen: the header of a change and the rail of its declared review steps.
 *
 * The header states the eight facts of the worktree it reads, each absence named with its
 * remedy: null is not zero, "could not be asked" is not "there is none", and no fact is ever
 * a tick. The rail below is the review steps as declared data — .harness/review.json decides
 * what draws, and a worktree offers its own branch's steps rather than the checkout's.
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

type Step = {
  key: string;
  title: string;
  asserts: string;
  implemented: boolean;
  marked: boolean;
};

/** The reading the rail draws itself from: declared steps, their marks' availability, the problem if any. */
type Steps = {
  path: string;
  steps: Step[];
  problem: string | null;
  marksNotAsked: string | null;
  issueKey: string | null;
  issueTitle: string | null;
  prompts: string[] | null;
};

const ink = {
  plain: "var(--muted-foreground)",
  warn: "var(--warn)",
  bad: "var(--bad)",
} as const;

export function App() {
  const [path, setPath] = useState(new URLSearchParams(window.location.search).get("path") ?? ".");
  const [reading, setReading] = useState<Header | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [stepped, setStepped] = useState<Steps | null>(null);
  const [step, setStep] = useState<string | null>(null);

  const read = useCallback(async (target: string) => {
    setLoading(true);
    setError(null);
    try {
      const [headerResponse, stepsResponse] = await Promise.all([
        fetch(`/api/header?path=${encodeURIComponent(target)}`),
        fetch(`/api/steps?path=${encodeURIComponent(target)}`),
      ]);
      if (!headerResponse.ok) {
        throw new Error(`the server said ${headerResponse.status}`);
      }
      setReading((await headerResponse.json()) as Header);
      setStepped(stepsResponse.ok ? (await stepsResponse.json()) as Steps : null);
      if (!stepsResponse.ok) {
        setError(`the steps could not be read (the server said ${stepsResponse.status})`);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "the header could not be read");
      setReading(null);
      setStepped(null);
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
          the reading could not be made: {error}
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

      {/* The step rail, drawn from the declaration: numbered boxes in declared order, tick from
          the issue's reviewed:<key> labels (read only), disabled with words when the panel this
          build would open does not exist. Changing .harness/review.json changes this rail. */}
      <div role="group" aria-label="The steps of a review" style={{ flex: "none", display: "flex", flexDirection: "column", gap: 8 }}>
        {stepped?.steps.map((entry, index) => (
          <button
            key={entry.key}
            type="button"
            aria-pressed={entry.key === step}
            disabled={!entry.implemented}
            title={entry.implemented ? entry.asserts : `${entry.asserts}\nnot implemented in this build (POC-02 and later)`}
            onClick={() => setStep(entry.key)}
            style={{ flex: "none", display: "flex", alignItems: "center", gap: 8, padding: "8px 10px", textAlign: "left", border: `1px solid ${entry.key === step ? "var(--primary)" : "var(--border)"}`, borderRadius: "var(--radius)", background: "var(--card)", color: entry.implemented ? "var(--foreground)" : "var(--muted-foreground)", fontSize: 13, fontWeight: 600 }}
          >
            <span aria-hidden style={{ width: 22, height: 22, display: "flex", alignItems: "center", justifyContent: "center", borderRadius: "50%", background: "var(--muted)", fontSize: 11 }}>{index + 1}</span>
            <span style={{ flex: 1, minWidth: 0 }}>{entry.title}</span>
            {!entry.implemented ? (
              <span style={{ fontSize: 11, fontWeight: 400, color: "var(--muted-foreground)" }}>not implemented</span>
            ) : entry.marked ? (
              <span title="marked by a reviewed:<key> label on the resolved issue" style={{ display: "flex", alignItems: "center", gap: 4, color: "var(--muted-foreground)" }}><Check size={14} aria-label="the issue carries this step's reviewed label" /> marked</span>
            ) : null}
          </button>
        ))}
        {stepped?.problem ? (
          <div role="note" style={{ padding: 14, borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", color: ink.warn, fontSize: 13 }}>{stepped.problem}</div>
        ) : null}
        {stepped && !stepped.problem && stepped.marksNotAsked ? (
          <div role="note" style={{ padding: 14, borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", color: ink.plain, fontSize: 13 }}>{stepped.marksNotAsked}</div>
        ) : null}
      </div>

      {/* The panel: one step at a time. Each step is one panel component file, mounted here in
          exactly one branch. */}
      <div style={{ flex: 1, minHeight: 120, padding: 14, borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", fontSize: 13, color: "var(--muted-foreground)", overflow: "auto" }}>
        {stepped?.steps.find((s) => s.key === step && s.implemented) ? (
          step === "proposal" ? <ProposalStep path={path} /> : step === "code" ? <CodeStep path={path} /> : step === "tests" ? <TestsStep path={path} hasPrompt={Boolean(stepped?.prompts?.includes(step))} /> : null
        ) : (
          <>
            <p style={{ margin: "0 0 6px" }}>The steps are not implemented yet — they arrive as declared data in POC-01, and each panel</p>
            <p style={{ margin: 0 }}>lands as its own component file mounted in one line above.</p>
          </>
        )}
      </div>
    </div>
  );
}
