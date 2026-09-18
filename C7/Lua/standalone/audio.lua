
local audio_map = {
  ["menu.main_menu_1"] = "Audio/Music/Icarus/Icarus_alt.ogg"
}

--[[
      Main audio override function
--]]
return function(civ3_audio)
  local oc3_audio = civ3_audio

  function oc3_audio.map_object_to_sprite(item)
    local value = audio_map[tostring(item)] or nil
    return value
  end

  return oc3_audio
end
