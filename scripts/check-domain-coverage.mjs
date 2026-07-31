import { readFileSync } from "node:fs";

const minimumLineRate = 0.9;
const minimumBranchRate = 0.85;
const coveragePath = process.argv[2];

if (!coveragePath) {
  console.error("Coverage file path is required.");
  process.exit(2);
}

const coverage = readFileSync(coveragePath, "utf8");
const summary = coverage.match(
  /<coverage\s+[^>]*line-rate="([0-9.]+)"[^>]*branch-rate="([0-9.]+)"[^>]*>/,
);

if (!summary) {
  console.error(`Coverage summary is missing from ${coveragePath}.`);
  process.exit(2);
}

const lineRate = Number(summary[1]);
const branchRate = Number(summary[2]);
console.log(
  `Domain coverage: line ${(lineRate * 100).toFixed(2)}%, branch ${(branchRate * 100).toFixed(2)}%.`,
);

if (lineRate < minimumLineRate || branchRate < minimumBranchRate) {
  console.error(
    "Domain coverage gate failed: required line 90.00% and branch 85.00%.",
  );
  process.exit(1);
}

console.log("Domain coverage gate passed.");
