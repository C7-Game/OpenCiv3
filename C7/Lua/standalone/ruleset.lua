--[[
The art replacements for units in the standalone mode.
A key represents a name of a unit prototype,
a value refers to a directory with unit art in C7/Assets/Art/Units
]]
local unit_replacement_art_map = {
  ["Settler"] = "Carthaginian Settler",
  ["Worker"] = "Carthaginian Worker",
  ["Scout"] = "Euro Scout",
  ["Explorer"] = "German Explorer",
  ["Warrior"] = "Tribal Mediterranean Warrior",
  ["Archer"] = "Chichimeca Archer",
  ["Spearman"] = "European Spearman",
  ["Swordsman"] = "European Swordsman",
  ["Chariot"] = "thracian chariot",
  ["Horseman"] = "Serbian Horseman",
  ["Pikeman"] = "Elf Spearman",
  ["Longbowman"] = "Longbowman",
  ["Musketman"] = "Generic Musketman 1600",
  ["Knight"] = "Medieval European Horse Spearman",
  ["Cavalry"] = "1750 British Dragoon",
  ["Catapult"] = "Dark Ages Onager",
  ["Cannon"] = "French Artillery 17th C",
  ["Galley"] = "Bireme",
  ["Caravel"] = "Cog",
  ["Frigate"] = "HeavyFrigate",
  ["Galleon"] = "East_Indiaman1",
  ["Privateer"] = "Pirate Ship",
  ["Medieval Infantry"] = "Gothic Swordsman",
  ["Trebuchet"] = "Medieval Trebuchet",
  ["Crusader"] = "Black Hospitaller Swordsman",
  ["Ancient Cavalry"] = "Oscan Companion",
  ["Curragh"] = "MinoanGalley",
}

--[[
This function defines the standalone addon to the base ruleset.

This function takes the initial save data from `civ3/ruleset.json` and
removes the units for which we don't have graphics replacements.

On the C# side, this function is called as part of addon loading logic
in GameModeLoader class
]]
return function(civ3_game_mode)
  local updated_unit_prototypes = {}

  for _, unit_prototype in ipairs(civ3_game_mode.unitPrototypes) do
    local replacement_art = unit_replacement_art_map[unit_prototype.name]

    -- Only preserve a unit if we have an art replacement for it
    if replacement_art then
      unit_prototype.art.mainArt.defaultName = replacement_art

      -- TODO: these are just placeholders until we have some proper art
      unit_prototype.art.pediaArt.large = "art\\civilopedia\\icons\\units\\unit_large.png"
      unit_prototype.art.pediaArt.small = "art\\civilopedia\\icons\\units\\unit_small.png"

      -- Remove the unit upgrade if we don't have a sprite for it
      local upgrade = unit_prototype.upgradesTo
      if not unit_replacement_art_map[upgrade] then
        unit_prototype.upgradesTo = nil
      end

      table.insert(updated_unit_prototypes, unit_prototype)
    end
  end

  civ3_game_mode.unitPrototypes = updated_unit_prototypes

  return civ3_game_mode
end
