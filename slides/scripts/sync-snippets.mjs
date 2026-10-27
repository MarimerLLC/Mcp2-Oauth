// Copies the sample source files that slides import with `<<<` into snippets/.
// Slidev only reads snippets from inside the deck folder, so slides can't point at ../src
// directly. Runs before dev, build, and export; restart the dev server to pick up edits.
import { copyFileSync, mkdirSync } from 'node:fs'
import { basename, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const deck = join(dirname(fileURLToPath(import.meta.url)), '..')
const sources = [
  '../src/Mcp2Oauth.McpServer/Program.cs',
  '../src/Mcp2Oauth.McpServer/Policies.cs',
  '../src/Mcp2Oauth.McpServer/Tools/ExpenseTools.cs',
]

const out = join(deck, 'snippets')
mkdirSync(out, { recursive: true })
for (const source of sources)
  copyFileSync(join(deck, source), join(out, basename(source)))
