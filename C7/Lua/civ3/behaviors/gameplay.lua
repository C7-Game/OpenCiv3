-- A generic lua script for orchestrating behaviours during gameplay.
-- The idea for this is to host script paths that are not serialized in the json,
-- but rather "hardcoded" in the code

local time_unit = ENUMS.TimeUnit

local function game_data()
  return GAME_DATA()
end

local function rules()
  return GAME_DATA().rules
end

local gameplay = {}

local function disband_reward(context)
  local unit = context
  local unit_shield_cost = unit.unitType.shieldCost;

  local has_city = unit.location.HasCity
  local is_unit_on_own_city = unit.location.OwningPlayer() == unit.owner

  if has_city and is_unit_on_own_city then
    local city = unit.location.owningCity;
    local item_produced = city.itemBeingProduced

    if (item_produced:GetType().Name == "Building" and item_produced.IsGreatWonder() == true) then
      return
    end

    if (item_produced:GetType().Name == "Inflow") then
      return
    end

    local reward = math.floor(unit_shield_cost * rules().ShieldRateForDisbanding)
    game_data().SendMessageToUiFromLua("Our " .. unit.name .. " has been converted to " .. reward .. " shields.",
      unit.location)
    city.SetStoredShields(reward, true)
  end
end

local function get_display_time_text(raw_time)
  local bc = game_data().timeOptions.negativeLabel
  local ad = game_data().timeOptions.positiveLabel

  -- Years
  if (game_data().timeOptions.baseUnit == time_unit.Years) then
    game_data().timeOptions.SetTimeUnitCurrent(time_unit.Years, raw_time)
    return math.abs(raw_time) .. " " ..
        (game_data().timeOptions.currentYear < 0 and bc or ad)
  end

  -- Months
  if (game_data().timeOptions.baseUnit == time_unit.Months) then
    local month = game_data().timeOptions.startMonth
    local start_year = game_data().timeOptions.startYear
    game_data().timeOptions.SetTimeUnitCurrent(time_unit.Years, start_year)
    if (raw_time > 12) then
      month = raw_time % 12
      game_data().timeOptions.SetTimeUnitCurrent(time_unit.Years, start_year + (raw_time / 12))
    else
      month = raw_time
      game_data().timeOptions.SetTimeUnitCurrent(time_unit.Months, month)
    end
    local month_name = game_data().timeOptions.GetAbbrMonthNameByIndex(month) -- can be overriden with a local function if needed
    return month_name .. ", " .. game_data().timeOptions.currentYear .. " " ..
        (game_data().timeOptions.currentYear < 0 and bc or ad)
  end

  -- Weeks
  if (game_data().timeOptions.baseUnit == time_unit.Weeks) then
    local week = game_data().timeOptions.startWeek
    local start_year = game_data().timeOptions.startYear
    game_data().timeOptions.SetTimeUnitCurrent(time_unit.Years, start_year)
    if (raw_time > 52) then
      week = raw_time % 52
      game_data().timeOptions.SetTimeUnitCurrent(time_unit.Years, start_year + (raw_time / 52))
    else
      week = raw_time
      game_data().timeOptions.SetTimeUnitCurrent(time_unit.Weeks, week)
    end
    return "Week " .. week .. ", " .. game_data().timeOptions.currentYear .. " " ..
        (game_data().timeOptions.currentYear < 0 and bc or ad)
  end

  -- Days
  if (game_data().timeOptions.baseUnit == time_unit.Days) then
    local day = game_data().timeOptions.startDay
    local start_year = game_data().timeOptions.startYear
    game_data().timeOptions.SetTimeUnitCurrent(time_unit.Years, start_year)
    if (raw_time > 365) then
      day = raw_time % 365
      game_data().timeOptions.SetTimeUnitCurrent(time_unit.Years, start_year + (raw_time / 365))
    else
      day = raw_time
      game_data().timeOptions.SetTimeUnitCurrent(time_unit.Days, day)
    end
    return "Day " .. day .. ", " .. game_data().timeOptions.currentYear .. " " ..
        (game_data().timeOptions.currentYear < 0 and bc or ad)
  end

  -- Hours
  if (game_data().timeOptions.baseUnit == time_unit.Hours) then
    local hour = game_data().timeOptions.startHour
    local start_day = game_data().timeOptions.startDay
    game_data().timeOptions.SetTimeUnitCurrent(time_unit.Days, start_day)
    if (raw_time > 24) then
      hour = raw_time % 24
      game_data().timeOptions.SetTimeUnitCurrent(time_unit.Days, start_day + (raw_time / 24))
    else
      hour = raw_time
      game_data().timeOptions.SetTimeUnitCurrent(time_unit.Hours, hour)
    end
    return "Hour " .. hour .. ", Day " .. game_data().timeOptions.currentDay
  end

  return "-12345 AD"
end

gameplay = {
  units = {
    -- Context = MapUnit mapUnit
    disband = function(context)
      disband_reward(context)
    end,
  },
  time = {
    -- Context = int time
    display_text = function(context)
      return get_display_time_text(context)
    end,
  },
  culture = {
    -- Context = CityBuilding building, GameData gameData, City city, Player player
    age_bonus = function(cityBuilding, gameData, city, player)
      if cityBuilding.building == nil or cityBuilding.baseCulturePerTurn <= 0 or cityBuilding.building.noAgeBonus then
        return 0
      end
      if gameData == nil or gameData.timeOptions == nil then
        return cityBuilding.baseCulturePerTurn
      end
      local building = cityBuilding.building
      if building.noAgeBonus then
        return cityBuilding.baseCulturePerTurn
      end
      local ageBonusRules = nil
      if gameData.rules ~= nil then
        local ok, value = pcall(function() return gameData.rules.ageBonus end)
        if ok then
          ageBonusRules = value
        end
      end
      if ageBonusRules ~= nil and ageBonusRules.enabled == false then
        return cityBuilding.baseCulturePerTurn
      end
      local currentYear = gameData.timeOptions.GetAbsoluteYear(gameData.turn)
      local ageInYears = currentYear - cityBuilding.year
      local yearsToDouble = 1000
      local multiplier = 2
      local repeatable = false
      if ageBonusRules ~= nil then
        if ageBonusRules.yearsToDouble and ageBonusRules.yearsToDouble > 0 then
          yearsToDouble = ageBonusRules.yearsToDouble
        end
        if ageBonusRules.multiplier and ageBonusRules.multiplier > 0 then
          multiplier = ageBonusRules.multiplier
        end
        if ageBonusRules.repeatable ~= nil then
          repeatable = ageBonusRules.repeatable
        end
      end
      if ageInYears < yearsToDouble then
        return cityBuilding.baseCulturePerTurn
      end
      if not repeatable then
        return cityBuilding.baseCulturePerTurn * multiplier
      end
      local doublings = 1 + math.floor((ageInYears - yearsToDouble) / yearsToDouble)
      local result = cityBuilding.baseCulturePerTurn
      for i = 1, doublings do
        result = result * multiplier
      end
      return result
    end,
  }
}

return gameplay
