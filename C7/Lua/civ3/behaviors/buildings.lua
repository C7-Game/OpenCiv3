local YieldType = ENUMS.Tile_YieldType

local function rules()
  return GAME_DATA().rules
end

local buildings = {}

buildings.production_rules = {
  must_be_coastal = function(city)
    return city.location:NeighborsOcean()
  end,

  must_be_near_river = function(city)
    return city.location:BordersRiver()
  end,

  -- Ocean doesn't count: the city needs a river, or a fresh water lake that it
  -- can work. A river on the city's own tile counts too.
  must_be_near_water = function(city)
    if city.location:BordersRiver() then
      return true
    end

    for _, tile in ipairs(city:GetWorkableTiles()) do
      if tile:BordersRiver() or (tile:IsWater() and tile.isFreshWater) then
        return true
      end
    end

    return false
  end,

  can_only_be_built_in_towns = function(city)
    return #city.residents <= rules().MaximumLevel1CitySize
  end,

  allows_city_size_2 = function(city)
    local is_town = #city.residents <= rules().MaximumLevel1CitySize
    local has_fresh_water = city.location:NeighborsFreshWater() or city.location:BordersRiver()
    return is_town and not has_fresh_water
  end,

  allows_city_size_3 = function(city)
    return #city.residents <= rules().MaximumLevel2CitySize
  end,
}

buildings.unit_production_effects = {
  veteran_ground_units = function(unit)
    if unit.unitType:IsLandUnit() then
      unit:Promote()
    end
  end,

  veteran_sea_units = function(unit)
    if unit.unitType:IsSeaUnit() then
      unit:Promote()
    end
  end,

  veteran_air_units = function(unit)
    if unit.unitType:IsAirUnit() then
      unit:Promote()
    end
  end,
}

buildings.tile_modifiers = {
  increases_food_in_water = function(yield)
    if yield.type == YieldType.Food and yield.tile:IsWater() and yield.baseYield > 0 then
      yield.bonus = yield.bonus + 1
    end
  end,

  increases_shields_in_water = function(yield)
    if yield.type == YieldType.Production and yield.tile:IsWater() then
      yield.bonus = yield.bonus + 1
    end
  end,

  increases_trade_in_water = function(yield)
    if yield.type == YieldType.Commerce and yield.tile:IsWater() and yield.baseYield > 0 then
      yield.bonus = yield.bonus + 1
    end
  end,
}

return buildings
