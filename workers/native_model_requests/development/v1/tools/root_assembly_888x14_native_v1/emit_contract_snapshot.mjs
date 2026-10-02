import { createHash } from 'node:crypto'
import { writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  nativeRecipeDigest,
} from '../../../../../../tools/locker_16029_native_recipe_registry.mjs'
import { buildNativeAssemblyContract } from '../../../../../../tools/lib/locker_16029_native_assembly_contract.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const recipeId = 'winnsen-16029-888w-14door-native-v1'
const expectedRecipeDigest = 'f07c5497f9de7d02727d8c084e5a7969a862c44c7990858cdef8e8e54feaceb8'

function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}

const recipe = TRUSTED_NATIVE_RECIPE_REGISTRY[recipeId]
if (!recipe || nativeRecipeDigest(recipe) !== expectedRecipeDigest) {
  throw new Error('trusted 888x14 recipe digest drifted; refusing to emit a stale assembly contract')
}

const contract = buildNativeAssemblyContract(recipe)
const text = `${JSON.stringify(stableValue(contract), null, 2)}\n`
const path = join(here, 'assembly_contract.snapshot.json')
writeFileSync(path, text, 'utf8')

process.stdout.write(`${JSON.stringify({
  path,
  recipeDigest: contract.recipe.digest,
  sha256: createHash('sha256').update(text).digest('hex').toUpperCase(),
})}\n`)
