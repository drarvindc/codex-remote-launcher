import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { inspectFuseWire } from "../runtime/src/check-package.mjs";

const sentinel = Buffer.from("dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX");

function fixture(nodeOptions, nodeCliInspect, prefix = Buffer.alloc(0)) {
  return Buffer.concat([prefix, sentinel, Buffer.from([1, 9]), Buffer.from(`11${nodeOptions}${nodeCliInspect}11111`)]);
}

const enabled = fixture("1", "1");
assert.equal(inspectFuseWire(enabled).classification, "Compatible");
assert.equal(inspectFuseWire(enabled).nodeCliInspect, "enabled");

const disabled = fixture("0", "0");
assert.equal(inspectFuseWire(disabled).classification, "IncompatibleMainInspectorDisabled");
assert.equal(inspectFuseWire(disabled).nodeOptions, "disabled");

const unknown = fixture("r", "r");
assert.equal(inspectFuseWire(unknown).classification, "Unknown");

const malformed = Buffer.concat([sentinel, Buffer.from([1, 9]), Buffer.from("111")]);
assert.equal(inspectFuseWire(malformed).classification, "Unknown");

const ambiguous = Buffer.concat([enabled, Buffer.from("padding"), enabled]);
assert.equal(inspectFuseWire(ambiguous).classification, "Unknown");

const before = createHash("sha256").update(disabled).digest("hex");
inspectFuseWire(disabled);
const after = createHash("sha256").update(disabled).digest("hex");
assert.equal(after, before);

console.log("FuseInspectionTests: PASS");
