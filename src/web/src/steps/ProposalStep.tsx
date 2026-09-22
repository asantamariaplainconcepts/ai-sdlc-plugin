import { useEffect, useState } from "react";
import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";

/**
 * The Proposal step: what the branch declares as its change — the live openspec/changes of the
 * worktree, read from the working tree, rendered as markdown.
 *
 * Every absence is a sentence: a branch with no change is an ordinary branch, and the panel names
 * the path where a declaration would be written. Several changes are drawn the same and neither
 * is marked as the one — which change a branch is about is not a fact on disk.
 */

type Artifact = { name: string; path: string };

type DeclaredChange = { name: string; path: string; artifacts: Artifact[] };

type Proposal = {
  path: string;
  problem: string | null;
  root: string | null;
  changes: DeclaredChange[];
};

type ArtifactRead = { path: string; text: string | null; problem: string | null };

export function ProposalStep({ path }: { path: string }) {
  const [proposal, setProposal] = useState<Proposal | null>(null);
  const [text, setText] = useState<ArtifactRead | null>(null);
  const [chosen, setChosen] = useState<string | null>(null);

  useEffect(() => {
    setProposal(null);
    setText(null);
    setChosen(null);
    fetch(`/api/proposal?path=${encodeURIComponent(path)}`)
      .then((r) => (r.ok ? (r.json() as Promise<Proposal>) : Promise.reject(new Error(`the server said ${r.status}`))))
      .then(setProposal, () => setProposal({ path, problem: "the declaration could not be read", root: null, changes: [] }));
  }, [path]);

  // The selection follows the list: an artifact that stops existing while somebody reads it
  // cannot stay chosen — fall back to the first present artifact, so the step opens on the
  // proposal, which is the one this step is named after.
  const artifacts = proposal?.changes.flatMap((change) => change.artifacts) ?? [];
  const reading = artifacts.find((artifact) => artifact.path === chosen) ?? artifacts[0];

  useEffect(() => {
    setText(null);
    if (!reading) {
      return;
    }

    fetch(`/api/artifact?file=${encodeURIComponent(reading.path)}`)
      .then((r) => (r.ok ? (r.json() as Promise<ArtifactRead>) : Promise.reject(new Error(`the server said ${r.status}`))))
      .then(setText, () => setText({ path: reading.path, text: null, problem: "the artifact could not be read" }));
  }, [reading?.path]);

  if (!proposal) {
    return <p style={{ margin: 0 }}>reading the declaration…</p>;
  }

  if (proposal.problem) {
    return <p style={{ margin: 0 }}>{proposal.problem}</p>;
  }

  if (proposal.changes.length === 0) {
    return (
      <p style={{ margin: 0 }}>
        this branch declares no change — it would be written at{" "}
        <span className="mono">
          {(proposal.root ?? proposal.path) + "/openspec/changes/<name>/proposal.md"}
        </span>
      </p>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10, minHeight: 0 }}>
      {proposal.changes.length > 1 ? (
        <p style={{ margin: 0, fontSize: 12, color: "var(--muted-foreground)" }}>
          {proposal.changes.length} changes are declared here, drawn the same — which one this branch is about is not a fact on disk
        </p>
      ) : null}

      {proposal.changes.map((change) => (
        <div key={change.path} style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          <span className="mono" style={{ fontSize: 11, color: "var(--muted-foreground)", overflowWrap: "anywhere" }}>
            {change.name}
          </span>
          <div role="group" aria-label={`artifacts of ${change.name}`} style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
            {change.artifacts.map((artifact) => (
              <button
                key={artifact.path}
                type="button"
                aria-pressed={artifact.path === (reading?.path ?? null)}
                onClick={() => setChosen(artifact.path)}
                className="mono"
                style={{
                  padding: "4px 10px",
                  borderRadius: "var(--radius)",
                  border: `1px solid ${artifact.path === (reading?.path ?? null) ? "var(--primary)" : "var(--border)"}`,
                  background: "var(--card)",
                  color: artifact.path === (reading?.path ?? null) ? "var(--foreground)" : "var(--muted-foreground)",
                  fontSize: 11,
                  cursor: "pointer",
                }}
              >
                {artifact.name}
              </button>
            ))}
          </div>
        </div>
      ))}

      <div style={{ borderTop: "1px solid var(--border)", paddingTop: 10, overflow: "auto" }}>
        {text?.problem ? (
          <p style={{ margin: 0, color: "var(--warn)" }}>{text.problem}</p>
        ) : text?.text == null ? (
          <p style={{ margin: 0 }}>reading {reading?.name}…</p>
        ) : (
          // The branch's own declaration, rendered as the markdown it is written in.
          <Markdown remarkPlugins={[remarkGfm]}>{text.text}</Markdown>
        )}
      </div>
    </div>
  );
}
