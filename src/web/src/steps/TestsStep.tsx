import { useCallback, useEffect, useState } from "react";

/**
 * The Tests step: run the step's declared prompt with `claude --print`, held to completion — no
 * streaming, no chat. The run list: exit code AND is_error (a failed task still exits 0), cost
 * absent stated rather than zero, transcript located by rule or named absent.
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

const row: React.CSSProperties = { display: "flex", flexWrap: "wrap", columnGap: 10, alignItems: "baseline", fontSize: 12, padding: "6px 0", borderTop: "1px solid var(--border)" };

export function TestsStep({ path, hasPrompt }: { path: string; hasPrompt: boolean }) {
  const [runs, setRuns] = useState<Run[] | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [launching, setLaunching] = useState(false);

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

  useEffect(() => {
    void list();
  }, [list]);

  const launch = async () => {
    setLaunching(true);
    try {
      const response = await fetch(`/api/runs?path=${encodeURIComponent(path)}&step=tests`, { method: "POST" });
      const body = (await response.json()) as { problem: string | null };
      if (body.problem) setProblem(body.problem);
      await list();
    } catch (e) {
      setProblem(e instanceof Error ? e.message : "the launch failed");
    } finally {
      setLaunching(false);
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
    </div>
  );
}
