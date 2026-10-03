local ICON_WIDTH = 32
local ICON_HEIGHT = 32
local ICONS_PER_ROW = 14

local unit_icons = {
  extra_data = {
    path = "Art/Units/units_32.pcx",
  },
}

-- Context - ItemContext (UnitPrototype proto, Player player)
function unit_icons:map_object_to_sprite(context)
  local proto = context.proto
  local player = context.player

  if (proto:GetType().Name ~= "UnitPrototype") then
    error "Expected a UnitPrototype object"
  end
  if (player:GetType().Name ~= "Player") then
    error "Expected a Player object"
  end

  local index = proto.art.thumbnailArt.defaultIndex

  local variations = proto.art.thumbnailArt.variations
  local key = player.eraCivilopediaName

  -- Scientific leaders have a single shared icon ("SCI") regardless of era.
  if (proto:HasAttribute("scientificLeader")) then
    key = "SCI"
  end

  if (variations and variations[key]) then
    index = variations[key]
  end

  local x = 1 + (ICON_WIDTH + 1) * (index % ICONS_PER_ROW)
  local y = 1 + (ICON_HEIGHT + 1) * math.floor(index / ICONS_PER_ROW)

  return {
    path = self.extra_data.path,
    crop_region = { x, y, ICON_WIDTH, ICON_HEIGHT },
  }
end

return unit_icons
