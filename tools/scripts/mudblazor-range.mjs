// WR-10's layer-2 runs (ADR-0205, ADR-0450's note of 2026-11-02): every stable MudBlazor version nuget.org lists
// within the range the Wrappers declare, one a line, oldest first. CI's mudblazor-range job runs each Wrapper's
// layer-2 suite at each of them.
//
//     node tools/scripts/mudblazor-range.mjs '[9.6.0,10.0.0)'
//
// The versions come from the package's registration, which says whether each is listed; a page the index does not
// inline is fetched. Anything the registration does not say refuses the run, rather than letting a version go
// unrun: a version left out is a version the Wrappers claim and nothing has run.

const REGISTRATION = 'https://api.nuget.org/v3/registration5-gz-semver2/mudblazor/index.json';

const args = process.argv.slice(2);
if (args.length !== 1) throw new Error('Name the range the Wrappers declare, as [9.6.0,10.0.0).');
const [range] = args;
const bounds = /^\[(\d+\.\d+\.\d+),(\d+\.\d+\.\d+)\)$/.exec(range);
if (bounds === null) throw new Error(`The range '${range}' is not written [floor,ceiling).`);
const [, floor, ceiling] = bounds;

/** @param {string} version @returns {number[]} */
const parts = (version) => version.split('.').map(Number);
/** @param {string} a @param {string} b @returns {number} */
const compare = (a, b) => {
    const x = parts(a);
    const y = parts(b);
    for (let i = 0; i < 3; i++) if (x[i] !== y[i]) return x[i] - y[i];
    return 0;
};

/** @param {string} url @returns {Promise<any>} */
async function read(url) {
    const response = await fetch(url);
    if (!response.ok) throw new Error(`${url} answered ${response.status}.`);
    return response.json();
}

const index = await read(REGISTRATION);
if (!Array.isArray(index.items)) throw new Error(`${REGISTRATION} is not a registration index: it has no 'items'.`);
const versions = [];
for (const page of index.items) {
    const leaves = Array.isArray(page.items) ? page.items : (await read(page['@id'])).items;
    if (!Array.isArray(leaves)) throw new Error(`A page of ${REGISTRATION} has no versions: ${page['@id']}.`);
    for (const leaf of leaves) {
        const entry = leaf.catalogEntry;
        if (typeof entry?.version !== 'string') throw new Error(`A version in ${page['@id']} has no catalog entry naming it.`);
        if (!/^\d+\.\d+\.\d+$/.test(entry.version)) continue; // a prerelease is not in the range the Wrappers claim
        if (entry.listed === false) continue;
        if (compare(entry.version, floor) >= 0 && compare(entry.version, ceiling) < 0) versions.push(entry.version);
    }
}
if (versions.length === 0) throw new Error(`nuget.org lists no stable MudBlazor within ${range}.`);
console.log(versions.toSorted(compare).join('\n'));
