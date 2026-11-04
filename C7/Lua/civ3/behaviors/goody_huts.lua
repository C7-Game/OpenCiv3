--[[
  Goody hut ("tribal village") rules.

  The defaults reproduce Civilization III (Conquests). Addons can change the
  numbers in goody_huts.config, or replace any function below.
--]]

local BarbarianActivity = ENUMS.BarbarianActivity

local goody_huts = {}

goody_huts.config = {
  placement = {
    -- Civ3 makes floor(tile_count / 32) placement attempts. Each attempt
    -- picks a uniformly random tile (repeats possible) and is not retried if
    -- the tile is unsuitable, so this is a cap rather than a target.
    tiles_per_attempt = 32,

    -- No two huts within this many tiles of each other, measured as a full
    -- square (3 = the 7x7 box Civ3 checks).
    min_spacing = 3,

    -- Civ3 places no huts when barbarians are off entirely. Sedentary
    -- barbarians still get huts.
    disabled_for = { BarbarianActivity.None },
  },

  pop = {
    -- Each row splits a d20 between the outcomes: the listed outcomes get
    -- that many faces each, and the faces left over go to the row's
    -- `default`.
    dice = 20,

    -- Row = difficulty index (0 = easiest) + 1 if the civ is NOT
    -- Expansionist, so Expansionists roll one row easier. Rows past the end
    -- give "nothing".
    rows = {
      [0] = { city = 2, tech = 7, gold = 3, settlers = 3, maps = 2, mercenaries = 2, default = "nothing" },
      [1] = { city = 2, tech = 6, gold = 3, settlers = 3, maps = 2, mercenaries = 2, default = "barbarians" },
      [2] = { city = 2, tech = 5, gold = 2, settlers = 2, maps = 2, mercenaries = 2, default = "barbarians" },
      [3] = { city = 1, tech = 4, gold = 2, settlers = 2, maps = 2, mercenaries = 2, default = "barbarians" },
      [4] = { city = 1, tech = 3, gold = 2, settlers = 2, maps = 2, mercenaries = 2, default = "barbarians" },
      [5] = { city = 1, tech = 2, gold = 1, settlers = 1, maps = 1, mercenaries = 1, default = "barbarians" },
      [6] = { city = 0, tech = 1, gold = 1, settlers = 1, maps = 1, mercenaries = 1, default = "barbarians" },
      [7] = { city = 0, tech = 0, gold = 1, settlers = 0, maps = 1, mercenaries = 1, default = "barbarians" },
      [8] = { city = 0, tech = 0, gold = 0, settlers = 0, maps = 1, mercenaries = 1, default = "barbarians" },
    },

    -- Outcomes listed here are never given; their faces are shared out
    -- among the outcomes that remain.
    --
    -- TODO: Civ3 disables "city" when a scenario rule equivalent to the
    -- "elimination" game setting is on. Add "city" here when that setting
    -- exists.
    disabled_outcomes = {},

    gold = {
      early_amount = 25,
      late_amount = 50,
      -- The late amount applies once the turn number is above this.
      late_after_turn = 49,
    },

    maps = {
      -- Tiles within this many tiles (as a full square) may be revealed.
      radius = 4,
      -- Each candidate tile is revealed with probability chance / out_of.
      chance = 4,
      out_of = 5,
      -- Only tiles on the hut's landmass, or of this terrain, are revealed.
      shore_terrain = "coast",
    },

    city = {
      -- From the decompiled code: only civs with the Expansionist trait get
      -- cities from huts.
      requires_expansionist = true,
      -- No city from a hut that is closer than this to one of the player's
      -- own cities (Manhattan distance).
      min_own_city_distance = 4,
      -- After this turn, the new city gets RNG(max_extra_citizens + 1)
      -- extra citizens.
      extra_citizens_after_turn = 49,
      max_extra_citizens = 3,
    },

    settlers = {
      unit = "Settler",
    },

    mercenaries = {
      -- TODO: Civ3 picks a unit that both the barbarians and the player can
      -- use, and that some player in the game can build (or has built).
      unit = "Warrior",
      -- Up to this many mercenaries appear, each on a different tile: the
      -- hut tile or one of its neighbors.
      max_units = 3,
    },

    tech = {
      -- Only players in this era or earlier get techs from huts.
      max_player_era = 0,
      -- Only techs from this era or earlier can be given.
      max_tech_era = 1,
      -- Civ3 gives the candidate with the lowest cost + RNG(cost_jitter).
      cost_jitter = 100,
    },

    barbarians = {
      -- No barbarians from a hut that is closer than this to one of the
      -- player's own cities (Manhattan distance).
      min_own_city_distance = 2,
      -- Up to this many barbarians appear next to the hut. The first gets a
      -- 3/4 chance per free tile, the next 2/3, then 1/2.
      max_units = 3,
    },
  },

  messages = {
    gold = "The tribal elders give us a gift of %d gold!",
    maps = "The villagers share their maps of the surrounding lands!",
    city = "The village joins our civilization as the city of %s!",
    nothing = "The village is deserted.",
    settlers = "The villagers have given us a settler!",
    mercenaries = "The villagers have given us a skilled warrior!",
    tech = "The villagers teach us the secrets of %s!",
    barbarians = "The village defy your colonial intent with their own attack!",
  },
}

local function contains(list, value)
  for _, v in ipairs(list) do
    if v == value then
      return true
    end
  end
  return false
end

goody_huts.placement = {}

-- Context = GoodyHutPlacementContext, tile = Tile
function goody_huts.placement.is_valid_site(context, tile)
  local config = goody_huts.config.placement

  if not tile:IsLand() then
    return false
  end

  if tile.hasGoodyHut or tile.hasBarbarianCamp or tile:HasResource() then
    return false
  end

  -- Civ3 also rejects tiles with a city, units, or claimed land. None of
  -- those exist yet during map generation, apart from the starting
  -- locations the units will be placed on.
  if tile:HasCity() or tile.owningCity ~= nil or #tile.unitsOnTile > 0 then
    return false
  end
  if context:IsStartingLocation(tile) then
    return false
  end

  for _, other in ipairs(tile:GetTilesWithinTileSquare(config.min_spacing)) do
    if other.hasGoodyHut then
      return false
    end
  end

  return true
end

-- Context = GoodyHutPlacementContext
--
-- Called once at the end of map generation. Draw random numbers from
-- context:Random(n), not math.random, so the huts depend only on the seed.
function goody_huts.placement.place(context)
  local config = goody_huts.config.placement

  if contains(config.disabled_for, context.barbarianActivity) then
    return
  end

  local tile_count = context.tileCount
  local attempts = math.floor(tile_count / config.tiles_per_attempt)

  for _ = 1, attempts do
    local tile = context:TileAtIndex(context:Random(tile_count))
    if goody_huts.placement.is_valid_site(context, tile) then
      tile.hasGoodyHut = true
    end
  end
end

-------------------------------------------------------------------------------
-- Popping a hut
--
-- Context = GoodyHutContext for every function below. Draw random numbers
-- from the global RNG(n), not math.random, so results follow the game seed.
-------------------------------------------------------------------------------

local function average_cities_per_civ(context)
  local civs = context:LivingCivCount()
  if civs <= 0 then
    return 0
  end
  return math.floor(context:WorldCityCount() / civs)
end

-- Civ3 withholds cities and settlers from players who already have more
-- cities than the world average.
local function not_ahead_in_cities(context)
  return average_cities_per_civ(context) >= context:OwnCityCount()
end

-- Techs the player could be given: unknown, early enough, and with every
-- prerequisite known.
local function hut_tech_candidates(context)
  local config = goody_huts.config.pop.tech
  local candidates = {}

  for _, tech in ipairs(context.gameData.techs) do
    if not context:KnowsTech(tech) and context:TechEraIndex(tech) <= config.max_tech_era then
      local prerequisites_known = true
      for _, prerequisite in ipairs(tech.Prerequisites) do
        if not context:KnowsTech(prerequisite) then
          prerequisites_known = false
          break
        end
      end
      if prerequisites_known then
        table.insert(candidates, tech)
      end
    end
  end

  return candidates
end

local function is_free_land_tile(tile)
  return tile ~= nil and tile:IsLand() and not tile:HasCity() and #tile.unitsOnTile == 0
end

-- Each outcome has can_apply (its preconditions) and apply (its effect).
goody_huts.outcomes = {}

goody_huts.outcomes.gold = {
  can_apply = function(context)
    return not context.tile:HasResource()
  end,
  apply = function(context)
    local config = goody_huts.config.pop.gold
    local amount = config.early_amount
    if context.turn > config.late_after_turn then
      amount = config.late_amount
    end
    context:GiveGold(amount)
    context:Notify(string.format(goody_huts.config.messages.gold, amount))
  end,
}

goody_huts.outcomes.maps = {
  can_apply = function(_)
    return true
  end,
  apply = function(context)
    local config = goody_huts.config.pop.maps
    local hut = context.tile

    for _, tile in ipairs(hut:GetTilesWithinTileSquare(config.radius)) do
      local on_hut_landmass = tile.continent == hut.continent
      local is_shore = tile.baseTerrainType.Key == config.shore_terrain
      if (on_hut_landmass or is_shore) and RNG(config.out_of) < config.chance then
        context:RevealTile(tile)
      end
    end

    context:Notify(goody_huts.config.messages.maps)
  end,
}

goody_huts.outcomes.city = {
  can_apply = function(context)
    local config = goody_huts.config.pop.city

    if config.requires_expansionist and not context:IsExpansionist() then
      return false
    end
    if context:OwnCityCount() < 1 or not not_ahead_in_cities(context) then
      return false
    end
    if context:DistanceToNearestOwnCity() < config.min_own_city_distance then
      return false
    end
    return context:CanFoundCity()
  end,
  apply = function(context)
    local config = goody_huts.config.pop.city
    local city = context:FoundCity()

    if context.turn > config.extra_citizens_after_turn then
      context:AddCitizens(city, RNG(config.max_extra_citizens + 1))
    end

    context:Notify(string.format(goody_huts.config.messages.city, city.name))
  end,
}

goody_huts.outcomes.nothing = {
  can_apply = function(_)
    return true
  end,
  apply = function(context)
    context:Notify(goody_huts.config.messages.nothing)
  end,
}

goody_huts.outcomes.settlers = {
  can_apply = function(context)
    -- Ignoring the settler in production prevents popping a settler rule
    return not_ahead_in_cities(context)
  end,
  apply = function(context)
    context:SpawnUnit(context.player, goody_huts.config.pop.settlers.unit, context.tile)
    context:Notify(goody_huts.config.messages.settlers)
  end,
}

-- Whether a land unit of the given type could stand on the tile alongside
-- the player's own units: passable land with no other player's units or
-- city on it.
local function can_hold_land_unit(context, tile, unit_type)
  if tile == nil or not tile:IsLand() or tile:IsImpassableTo(unit_type) then
    return false
  end
  if tile:HasCity() and tile.cityAtTile.owner ~= context.player then
    return false
  end
  for _, unit in ipairs(tile.unitsOnTile) do
    if unit.owner ~= context.player then
      return false
    end
  end
  return true
end

goody_huts.outcomes.mercenaries = {
  can_apply = function(context)
    return context:FindUnitPrototype(goody_huts.config.pop.mercenaries.unit) ~= nil
  end,
  apply = function(context)
    local config = goody_huts.config.pop.mercenaries
    local unit_type = context:FindUnitPrototype(config.unit)

    -- The hut tile and its 8 neighbors, in a fixed order.
    local candidates = {}
    local tiles = { context.tile }
    for i = 0, 7 do
      table.insert(tiles, context:NeighborAt(i))
    end
    for i = 1, 9 do
      if can_hold_land_unit(context, tiles[i], unit_type) then
        table.insert(candidates, tiles[i])
      end
    end

    -- Pick up to max_units different tiles at random.
    for _ = 1, math.min(config.max_units, #candidates) do
      local tile = table.remove(candidates, RNG(#candidates) + 1)
      context:SpawnUnitOfType(context.player, unit_type, tile)
    end

    context:Notify(goody_huts.config.messages.mercenaries)
  end,
}

goody_huts.outcomes.tech = {
  can_apply = function(context)
    if context:PlayerEraIndex() > goody_huts.config.pop.tech.max_player_era then
      return false
    end
    return #hut_tech_candidates(context) > 0
  end,
  apply = function(context)
    local config = goody_huts.config.pop.tech
    local best, best_score = nil, nil

    for _, tech in ipairs(hut_tech_candidates(context)) do
      local score = tech.Cost + RNG(config.cost_jitter)
      if best == nil or score < best_score then
        best, best_score = tech, score
      end
    end

    context:GrantTech(best)
    context:Notify(string.format(goody_huts.config.messages.tech, best.Name))
  end,
}

goody_huts.outcomes.barbarians = {
  can_apply = function(context)
    local config = goody_huts.config.pop.barbarians

    if context:IsExpansionist() or context:OwnCityCount() < 1 then
      return false
    end
    if context:DistanceToNearestOwnCity() < config.min_own_city_distance then
      return false
    end
    -- Units that treat all terrain as roads (explorers) are safe.
    if context.unit.unitType.allTerrainAsRoads then
      return false
    end
    return context:BarbarianPlayer() ~= nil
  end,
  apply = function(context)
    local config = goody_huts.config.pop.barbarians
    local barbarians = context:BarbarianPlayer()
    local unit_type = context.gameData.barbarianInfo.basicBarbarian

    -- Walk the 8 neighbors, starting at a direction that depends on the
    -- turn. Each free land tile gets a barbarian with chance
    -- (slots - 1) / slots, and every barbarian placed uses up a slot.
    local slots = config.max_units + 1
    for i = 0, 7 do
      if slots <= 1 then
        break
      end
      local tile = context:NeighborAt(context.turn + i)
      if is_free_land_tile(tile) and RNG(slots) < slots - 1 then
        context:SpawnUnitOfType(barbarians, unit_type, tile)
        slots = slots - 1
      end
    end

    context:Notify(goody_huts.config.messages.barbarians)
  end,
}

-- The order outcomes are listed in when rolling. Any order gives the same
-- odds; a fixed one keeps the results reproducible from the seed.
local outcome_order = { "city", "tech", "gold", "settlers", "maps", "mercenaries" }

-- Returns a list of { name, weight } for the hut's difficulty row, before
-- preconditions are checked.
function goody_huts.weights(context)
  local config = goody_huts.config.pop

  local row_index = context:DifficultyIndex()
  if not context:IsExpansionist() then
    row_index = row_index + 1
  end

  local row = config.rows[row_index]
  if row == nil then
    return { { "nothing", 1 } }
  end

  local weights = {}
  local used = 0
  for _, name in ipairs(outcome_order) do
    local weight = row[name] or 0
    table.insert(weights, { name, weight })
    used = used + weight
  end
  table.insert(weights, { row.default, config.dice - used })

  return weights
end

-- Picks an outcome name. Civ3 re-rolls when an outcome's preconditions
-- fail; drawing once among only the outcomes that can happen gives the same
-- odds.
function goody_huts.roll(context)
  local config = goody_huts.config.pop
  local candidates = {}
  local total = 0

  for _, entry in ipairs(goody_huts.weights(context)) do
    local name, weight = entry[1], entry[2]
    local outcome = goody_huts.outcomes[name] or error("Unknown goody hut outcome: " .. tostring(name))

    if weight > 0 and not contains(config.disabled_outcomes, name) and outcome.can_apply(context) then
      table.insert(candidates, { name, weight })
      total = total + weight
    end
  end

  if total == 0 then
    return "nothing"
  end

  local face = RNG(total)
  for _, entry in ipairs(candidates) do
    face = face - entry[2]
    if face < 0 then
      return entry[1]
    end
  end
end

-- Called when a non-barbarian unit enters a tile with a hut. The hut has
-- already been removed. Returns the name of the outcome that was applied.
function goody_huts.pop(context)
  local name = goody_huts.roll(context)
  goody_huts.outcomes[name].apply(context)
  return name
end

return goody_huts
