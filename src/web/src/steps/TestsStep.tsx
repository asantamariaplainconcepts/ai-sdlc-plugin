import { useCallback, useEffect, useState } from "react";

/**
 * The Tests step: run the step's declared prompt with `claude --print`, held to completion — no
 * streaming, no chat. The run list: exit code AND is_error (a failed task still exits 0), cost
 * absent stated rather than zero, transcript located by rule or named absent.
 *
 * Below it, the gates of `.harness/commands.json` ("gate": true): each declared gate with its
 * reading — pass / fail / no concluyente, never a tick — the commit short-hash it was recorded
 * against, a stale marker when HEAD has moved since, and the bounded tail of what it printed.
 */

type Run = {
  sessionId: string;
  startingCommit: string | null;
  startedAt: string;
  exitCode: number | null;
  isError: boolean | null;
  costUsd: number | null;
  transcriptLocated: boolean;
  resultSummary: string | null;
  problem: string | null;
};

/** One declared gate and its latest recorded outcome; reading null = never ran or refuted. */
type Gate = {
  key: string;
  name: string;
  missing: boolean;
  missingSentence: string | null;
  reading: "pass" | "fail" | "inconclusive" | null;
  exitCode: number | null;
  commit: string | null;
  stale: boolean;
  freshness: string;
  finishedAt: string | null;
  tail: string | null;
  problem: string | null;
};

const row: React.CSSProperties = { display: "flex", flexWrap: "wrap", columnGap: 10, alignItems: "baseline", fontSize: 12, padding: "6px 0", borderTop: "1px solid var(--border)" };

/** pass = foreground, fail = bad, inconclusive = warn, never ran = warn (silence is failure to
 *  look, POC-05); missing is colored by the caller — bad, not a reading. */
const readingColor = (reading: Gate["reading"], missing: boolean): string | undefined =>
  reading === "pass" ? "var(--foreground)"
  : reading === null
    ? missing ? undefined : "var(--warn)"
    : reading === "fail" ? "var(--bad)"
    : "var(--warn)";

const readingText = (g: Gate): string =>
  g.missing ? "refuted at declaration" : g.reading === null ? "not run here" : g.reading === "inconclusive" ? "no concluyente" : g.reading;

export function TestsStep({ path, hasPrompt }: { path: string; hasPrompt: boolean }) {
  const [runs, setRuns] = useState<Run[] | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [launching, setLaunching] = useState(false);
  const [gates, setGates] = useState<Gate[] | null>(null);
  const [gatesProblem, setGatesProblem] = useState<string | null>(null);
  const [runningGates, setRunningGates] = useState(false);

  const list = useCallback(async () => {
    try {
      const response = await fetch(`/api/runs?path=${encodeURIComponent(path)}`);
      const body = (await response.json()) as { problem: string | null; runs: Run[] };
      setRuns(body.runs);
      setProblem(body.problem);
    } catch {
      setRuns(null);
    }
  }, [path]);

  const listGates = useCallback(async () => {
    try {
      const response = await fetch(`/api/gates?path=${encodeURIComponent(path)}`);
      const body = (await response.json()) as { problem: string | null; gates: Gate[] };
      setGates(body.gates);
      setGatesProblem(body.problem);
    } catch {
      setGates(null);
    }
  }, [path]);

  useEffect(() => {
    void list();
    void listGates();
  }, [list, listGates]);

  const launch = async () => {
    setLaunching(true);
    try {
      // trigger=button said out loud: the same contract the poller calls with trigger=poll — the
      // view does not read the value back, the record is where the divergence lives.
      const response = await fetch(`/api/runs?path=${encodeURIComponent(path)}&step=tests&trigger=button`, { method: "POST" });
      const body = (await response.json()) as { problem: string | null };
      if (body.problem) setProblem(body.problem);
      await list();
    } catch (e) {
      setProblem(e instanceof Error ? e.message : "the launch failed");
    } finally {
      setLaunching(false);
    }
  };

  const runGates = async () => {
    setRunningGates(true);
    try {
      const response = await fetch(`/api/gates?path=${encodeURIComponent(path)}`, { method: "POST" });
      const body = (await response.json()) as { problem: string | null; gates: Gate[] };
      setGates(body.gates);
      setGatesProblem(body.problem);
    } catch (e) {
      setGatesProblem(e instanceof Error ? e.message : "the gates could not run");
    } finally {
      setRunningGates(false);
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <div style={{ display: "flex", gap: 8, alignItems: "baseline", flexWrap: "wrap" }}>
        <button type="button" onClick={() => void launch()} disabled={launching || !hasPrompt}
          style={{ padding: "6px 12px", borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", cursor: "pointer" }}>
          {launching ? "running — held to completion…" : "run the tests step"}
        </button>
        {!hasPrompt ? (
          <span style={{ fontSize: 12, color: "var(--muted-foreground)" }}>
            no prompt is declared for this step — add one under "prompts" in .harness/review.json
          </span>
        ) : null}
      </div>

      {problem ? <div role="note" style={{ fontSize: 12, color: "var(--warn)" }}>{problem}</div> : null}
      {runs?.length === 0 && !problem ? <p style={{ margin: 0, fontSize: 12 }}>No run recorded yet.</p> : null}
      {(runs ?? []).map((r) => (
        <div key={r.sessionId} className="mono" style={row}>
          <span style={{ color: "var(--foreground)" }}>{r.startedAt.replace("T", " ").slice(0, 19)}</span>
          <span>session {r.sessionId.slice(0, 8)}</span>
          <span>from {r.startingCommit?.slice(0, 8) ?? "no commit"}</span>
          <span style={{ color: r.exitCode === 0 ? "var(--foreground)" : "var(--bad)" }}>exit {r.exitCode ?? "never ran"}</span>
          <span>provider error: {r.isError === null ? "not said" : r.isError ? "yes" : "no"}</span>
          <span>{r.costUsd === null ? "cost not given — the provider said nothing about it" : `$${Number(r.costUsd).toFixed(2)}`}</span>
          <span>{r.transcriptLocated ? "transcript at the rule's path" : "transcript absent — the derived path is not there"}</span>
          {r.problem ? <span style={{ flexBasis: "100%", color: "var(--warn)" }}>{r.problem}</span> : null}
          {r.resultSummary ? <span style={{ flexBasis: "100%", color: "var(--muted-foreground)" }}>{r.resultSummary.slice(0, 160)}</span> : null}
        </div>
      ))}

      {/* The gates, tied to the commit: run button, one row per declared gate. */}
      <div style={{ display: "flex", gap: 8, alignItems: "baseline", flexWrap: "wrap", marginTop: 8 }}>
        <button type="button" onClick={() => void runGates()} disabled={runningGates}
          style={{ padding: "6px 12px", borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", cursor: "pointer" }}>
          {runningGates ? "gates running — held to completion…" : "run the declared gates"}
        </button>
        <span style={{ fontSize: 12, color: "var(--muted-foreground)" }}>exit codes recorded against the commit, invalidated when HEAD moves</span>
      </div>

      {gatesProblem ? <div role="note" style={{ fontSize: 12, color: "var(--warn)" }}>{gatesProblem}</div> : null}
      {gates?.length === 0 && !gatesProblem ? (
        <p style={{ margin: 0, fontSize: 12 }}>no gates declared — declare one in .harness/commands.json with "gate": true</p>
      ) : null}
      {(gates ?? []).map((g) => (
        <div key={g.key} className="mono" style={row}>
          <span style={{ color: "var(--foreground)", fontWeight: 600 }}>{g.name}</span>
          <span style={{ color: readingColor(g.reading, g.missing) ?? (g.missing ? "var(--bad)" : "var(--muted-foreground)") }}>
            {readingText(g)}
            {g.missing ? " — could not run" : ""}
          </span>
          {g.exitCode !== null ? <span>exit {g.exitCode}</span> : null}
          {g.finishedAt ? <span>{g.finishedAt.replace("T", " ").slice(0, 19)}</span> : null}
          <span>on {g.commit?.slice(0, 8) ?? "no commit"}</span>
          {g.stale ? (
            <span style={{ color: "var(--warn)" }} title={`recorded against ${g.commit}, the worktree has moved since`}>
              stale — HEAD has moved since the recording
            </span>
          ) : null}
          {g.missing && g.missingSentence ? <span style={{ flexBasis: "100%", color: "var(--bad)" }}>{g.missingSentence}</span> : null}
          {g.problem ? <span style={{ flexBasis: "100%", color: "var(--warn)" }}>{g.problem}</span> : null}
          {g.tail ? (
            <details style={{ flexBasis: "100%" }}>
              <summary style={{ cursor: "pointer", color: "var(--muted-foreground)" }}>the tail of what it printed</summary>
              <pre style={{ margin: "6px 0 0", whiteSpace: "pre-wrap", fontSize: 11, color: "var(--muted-foreground)" }}>{g.tail}</pre>
            </details>
          ) : null}
        </div>
      ))}
    </div>
  );
}
