local terrain = {}

local TERRAIN = "Art/Terrain/"

terrain.base = {
  extra_data = {
    -- A triple sheet is a sprite sheet containing sprites for three different terrain types including transitions between.
    triple_sheets = {
      TERRAIN .. "xtgc.pcx",
      TERRAIN .. "xpgc.pcx",
      TERRAIN .. "xdgc.pcx",
      TERRAIN .. "xdpc.pcx",
      TERRAIN .. "xdgp.pcx",
      TERRAIN .. "xggc.pcx",
      TERRAIN .. "wCSO.pcx",
      TERRAIN .. "wSSS.pcx",
      TERRAIN .. "wOOO.pcx",
    },
  },
}

function terrain.base:map_object_to_sprite(tile)
  if tile:GetType().Name ~= "Tile" then
    error "Expected a Tile object"
  end

  local terrain_image_id = tile.ExtraInfo.BaseTerrainImageID
  local terrain_file_id = tile.ExtraInfo.BaseTerrainFileID

  local sprite_width = 128
  local sprite_height = 64

  local x_sheet = terrain_image_id % 9
  local y_sheet = math.floor(terrain_image_id / 9)

  return {
    path = self.extra_data.triple_sheets[terrain_file_id + 1]
      or error("Unknown BaseTerrainFileID: " .. terrain_file_id),
    crop_region = {
      x_sheet * sprite_width,
      y_sheet * sprite_height,
      sprite_width,
      sprite_height,
    },
    shadows = false,
  }
end

terrain.mountain = {
  base = TERRAIN .. "Mountains.pcx",
  jungle = TERRAIN .. "mountain jungles.pcx",
  snow = TERRAIN .. "Mountains-snow.pcx",
  forest = TERRAIN .. "mountain forests.pcx",
}

terrain.hill = {
  base = TERRAIN .. "xhills.pcx",
  forest = TERRAIN .. "hill forests.pcx",
  jungle = TERRAIN .. "hill jungle.pcx",
}

terrain.volcano = {
  base = TERRAIN .. "Volcanos.pcx",
  forest = TERRAIN .. "Volcanos forests.pcx",
  jungle = TERRAIN .. "Volcanos jungles.pcx",
}

terrain.barbarian_camp = {
  path = TERRAIN .. "TerrainBuildings.pcx",
  crop_region = { 256, 0, 128, 64 },
  shadows = false,
}

-- goodyhuts.pcx is a 3x3 grid of 128x64 sprites holding 8 hut variants
-- (the last cell is empty).
terrain.goody_hut = {
  extra_data = {
    path = TERRAIN .. "goodyhuts.pcx",
    variants = 8,
    columns = 3,
  },
}

function terrain.goody_hut:map_object_to_sprite(_)
  -- The variant is purely cosmetic, so it is picked at random and does not
  -- need to be reproducible from the seed (math.random is fine here, unlike
  -- in gameplay scripts). MapView caches the texture per tile, so a hut
  -- keeps the same look for the rest of the session; it may change after
  -- reloading a save.
  local variant = math.random(0, self.extra_data.variants - 1)
  local column = variant % self.extra_data.columns
  local row = math.floor(variant / self.extra_data.columns)

  return {
    path = self.extra_data.path,
    crop_region = { column * 128, row * 64, 128, 64 },
  }
end

terrain.marsh = {
  large = {
    path = TERRAIN .. "marsh.pcx",
    crop_region = { 0, 0, 512, 176 },
    shadows = false,
  },
  small = {
    path = TERRAIN .. "marsh.pcx",
    crop_region = { 0, 176, 640, 176 },
    shadows = false,
  },
}

terrain.river = TERRAIN .. "mtnRivers.pcx"

terrain.river_delta = TERRAIN .. "deltaRivers.pcx"

terrain.tnt = TERRAIN .. "tnt.pcx"

terrain.jungle = {
  large = {
    path = TERRAIN .. "grassland forests.pcx",
    crop_region = { 0, 0, 512, 176 },
    shadows = false,
  },
  small = {
    path = TERRAIN .. "grassland forests.pcx",
    crop_region = { 0, 176, 768, 176 },
    shadows = false,
  },
}

terrain.forest = {
  large = {
    path = TERRAIN .. "grassland forests.pcx",
    crop_region = { 0, 352, 512, 176 },
    shadows = false,
  },
  small = {
    path = TERRAIN .. "grassland forests.pcx",
    crop_region = { 0, 528, 640, 176 },
    shadows = false,
  },
  plains = {
    large = {
      path = TERRAIN .. "plains forests.pcx",
      crop_region = { 0, 352, 512, 176 },
      shadows = false,
    },
    small = {
      path = TERRAIN .. "plains forests.pcx",
      crop_region = { 0, 528, 640, 176 },
      shadows = false,
    },
  },
  tundra = {
    large = {
      path = TERRAIN .. "tundra forests.pcx",
      crop_region = { 0, 352, 512, 176 },
      	shadows = false,
    },
    small = {
      path = TERRAIN .. "tundra forests.pcx",
      crop_region = { 0, 528, 640, 176 },
      	shadows = false,
    },
  },
}

terrain.pine = {
  forest = {
    path = TERRAIN .. "grassland forests.pcx",
    crop_region = { 0, 704, 768, 176 },
    shadows = false,
  },
  plains = {
    path = TERRAIN .. "plains forests.pcx",
    crop_region = { 0, 704, 768, 176 },
    shadows = false,
  },
  tundra = {
    path = TERRAIN .. "tundra forests.pcx",
    crop_region = { 0, 704, 768, 176 },
    shadows = false,
  },
}

terrain.fog_of_war = {
  path = TERRAIN .. "FogOfWar.pcx",
  pure_alpha = true,
}

return terrain
