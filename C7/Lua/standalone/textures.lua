--[[
  To add new replacement texture to the config modify the "c7_texture_list" table.
--]]
local utils = require "utils"

local c7_texture_list = {
  "Art/buttonsFINAL.png",
  "Art/TileInfo.png",
  "Art/X-o_ALLstates-sprite.png",
  "Art/SmallHeads/popHeads.png",
  "Art/Terrain/Mountains-snow.png",
  "Art/Terrain/Mountains.png",
  "Art/Terrain/TerrainBuildings.png",
  "Art/Terrain/Volcanos forests.png",
  "Art/Terrain/Volcanos jungles.png",
  "Art/Terrain/Volcanos.png",
  "Art/Terrain/grassland forests.png",
  "Art/Terrain/hill forests.png",
  "Art/Terrain/hill jungle.png",
  "Art/Terrain/irrigation DESETT.png",
  "Art/Terrain/irrigation PLAINS.png",
  "Art/Terrain/irrigation TUNDRA.png",
  "Art/Terrain/irrigation.png",
  "Art/Terrain/marsh.png",
  "Art/Terrain/mountain forests.png",
  "Art/Terrain/mountain jungles.png",
  "Art/Terrain/mtnRivers.png",
  "Art/Terrain/deltaRivers.png",
  "Art/Terrain/plains forests.png",
  "Art/Terrain/tnt.png",
  "Art/Terrain/roads.png",
  "Art/Terrain/railroads.png",
  "Art/Terrain/tundra forests.png",
  "Art/Terrain/wCSO.png",
  "Art/Terrain/wOOO.png",
  "Art/Terrain/wSSS.png",
  "Art/Terrain/xdgc.png",
  "Art/Terrain/xdgp.png",
  "Art/Terrain/xdpc.png",
  "Art/Terrain/xggc.png",
  "Art/Terrain/xhills.png",
  "Art/Terrain/xpgc.png",
  "Art/Terrain/xtgc.png",
  "Art/Terrain/craters.png",
  "Art/Terrain/pollution.png",
  "Art/Cities/rMIDEAST.png",
  "Art/Cities/MIDEASTWALL.png",
  "Art/Cities/DESTROY.png",
  "Art/city screen/CityIcons.png",
  "Art/city screen/ProdButton.png",
  "Art/city screen/background.png",
  "Art/city screen/cityMgmtButtons.png",
  "Art/resources.png",
  "Art/WorldSetup/CLIMTEMPAGEDepress.png",
  "Art/WorldSetup/CLIMTEMPAGERollovers.png",
  "Art/WorldSetup/age.png",
  "Art/WorldSetup/background.png",
  "Art/WorldSetup/climate.png",
  "Art/WorldSetup/landmassWaterSMALL.png",
  "Art/WorldSetup/landmassWaterSMALLdepress.png",
  "Art/WorldSetup/landmassWaterSMALLrollovers.png",
  "Art/WorldSetup/landmassWaterlarge.png",
  "Art/WorldSetup/temperature.png",
  "Art/interface/NormButtons.png",
  "Art/interface/rolloverbuttons.png",
  "Art/interface/highlightedbuttons.png",
  "Art/interface/box left color.png",
  "Art/interface/box right color.png",
  "Art/interface/nextturn states color.png",
  "Art/interface/consoleButtons.png",
  "Art/SmallHeads/popupDOMESTIC.png",
  "Art/SmallHeads/popupTRADE.png",
  "Art/SmallHeads/popupMILITARY.png",
  "Art/SmallHeads/popupFOREIGN.png",
  "Art/SmallHeads/popupCULTURE.png",
  "Art/SmallHeads/popupSCIENCE.png",
  "Art/Advisors/domestic_icons_aux.png",
  "Art/Advisors/domesticBUTTON.png",
  "Art/Cities/city icons.png",
  "Art/popupborders.png",
  "Art/interface/menuButtons.png",
  "Art/Advisors/dialogbox.png",
  "Art/Advisors/domestic.png",
  "Art/Advisors/domestic_plusminus.png",
  "Art/Tech Chooser/scienceNAV.png",
  "Art/Credits/credits_background.png",
  "Art/city screen/ProductionQueueBox.png",
  "Art/Advisors/non_required.png",
  "Art/Advisors/techboxes.png",
  "Art/Advisors/military.png",
  "Art/interface/MovementLED.png",
  "Art/Advisors/science_ancient.png",
  "Art/Advisors/science_middle.png",
  "Art/Advisors/science_industrial_new.png",
  "Art/Advisors/science_modern.png",
  "Art/exitBox-backgroundStates.png",
  "Art/PlayerSetup/playerSetup.png",
  "Art/Diplomacy/talk_offer.png",
  "Art/Diplomacy/counter.png",
  "Art/PalaceView/bkgr.png",
  "Art/city screen/luxuryicons_small.png",
  "Art/Terrain/FogOfWar.png",
  "Art/Terrain/Territory.png",
  "Art/Units/units_32.png",
  "Art/city screen/buildings-small.png",
  "Art/city screen/buildings-large.png",
  "Art/Cursor.png",
  "Art/SCROLL.png",
  "Art/interface/box trans color.png",
  "Art/SpaceShip/SHIPbackground.png",
  "Art/Advisors/trade.png",
  "Art/Advisors/foreign.png",
  "Art/Advisors/foreignTAB.png",
  "Art/Advisors/culture.png",
  "Art/Advisors/demographics.png",
  "Art/Advisors/wonders_background.png",
  "Art/Advisors/domestic_plusminus.png",
  "Art/histograph-win5_final.png",
  "Art/histograph-top5_final.png",
  "Art/3checkboxes-USE.png"
}

--- For ease of editing, we define the civ colors as hex codes, not 1x1 px images
local civ_colors = {
  "F0F8FF", -- Alice Blue (whiteish)
  "E6194B", -- Red
  "F58231", -- Orange
  "FFE119", -- Yellow
  "3CB44B", -- Green
  "4363D8", -- Blue
  "000075", -- Navy
  "FABED4", -- Pink
  "911EB4", -- Purple
  "9A6324", -- Brown
  "AAFFC3", -- Mint
  "42D4F4", -- Cyan
  "F032E6", -- Magenta
  "808000", -- Olive
  "DCBEFF", -- Lavender
  "A9A9A9", -- Grey
  "008080", -- Teal
  "FFD700", -- Gold
  "800000", -- Maroon
  "00FF00", -- Lime
  "FFC0CB", -- Hot Pink
  "4682B4", -- Steel Blue
  "D2B48C", -- Tan
  "FF7F50", -- Coral
  "6A5ACD", -- Slate Blue
  "2E8B57", -- Sea Green
  "DAA520", -- Goldenrod
  "C71585", -- Medium Violet Red
  "556B2F", -- Dark Olive Green
  "8B4513", -- Saddle Brown
  "B0C4DE", -- Light Steel Blue
  "696969"  -- Dim Gray
}

local tech_icon_replacement_map = {
  ["tech-2"] = "Masonry.png",
  ["tech-3"] = "Alphabet.png",
  ["tech-5"] = "TheWheel.png",
  ["tech-6"] = "WarriorCode.png",
  ["tech-7"] = "CeremonialBurial.png",
  ["tech-10"] = "Mysticism.png",
  ["tech-13"] = "Code of Laws.png",
  ["tech-14"] = "Literature.png",
  ["tech-15"] = "MapMaking.png",
  ["tech-16"] = "HorsebackRiding.png",
}


-- Build lookup table from c7_texture_list without extensions
local lookup = {}
for _, path in ipairs(c7_texture_list) do
  lookup[utils.strip_extension(path)] = path
end

local function path_transformer(path)
  local stripped = utils.strip_extension(path)
  return lookup[stripped] or path
end


--[[
  This function returns a table with texture definitions for "modern" graphics.

  It produces this table by copying the config of the civ3 assets and
  replacing the paths to the original Civilization 3 PCX textures with
  modern PNG versions.
--]]
return function(civ3_textures)
  local c7_textures = utils.transform_paths(civ3_textures, path_transformer)

  -- TODO: Add proper replacement for the asset
  c7_textures.terrain.river_delta = "Art/Terrain/mtnRivers.png"

  c7_textures.civ_colors = {}
  for i, hex_color in ipairs(civ_colors) do
    c7_textures.civ_colors["color_" .. (i-1)] = { path = "", hex_color = hex_color }
  end

  c7_textures.animations.cursor = {
    path = "Art/Animations/Cursor.png",
    animation_rows = 2,
    animation_cols = 9,
    frame_duration = 0.6,
  }
  c7_textures.animations.disorder = {
    path = "Art/Animations/DisorderDefault.png",
    animation_rows = 1,
    animation_cols = 6,
    frame_duration = 0.6,
  }

  function c7_textures.tech_icons.small:map_object_to_sprite(tech)
    local icon = tech_icon_replacement_map[tostring(tech.id)] or "placeholder.png"
    return { path = "Art/Tech Chooser/Icons/" .. icon }
  end
  
  function c7_textures.leader_heads:map_object_to_sprite(player_or_civ)
    return {
      path = "Art/Advisors/placeholder_leaderhead.png",
    }
  end

  function c7_textures.borders:find_column(_)
    return 0
  end

  return c7_textures
end
