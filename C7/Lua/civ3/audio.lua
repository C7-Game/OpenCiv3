-- Base paths
local SOUNDS = "Sounds/"
local MENU = SOUNDS .. "Menu/"
local AMBIENCE = SOUNDS .. "Ambience Sfx/"

-- Audio definitions
local audio = {}

audio.ambience = {
  oriole = AMBIENCE .. "Oriole.wav"
}

audio.buttons = {
  button_1 = SOUNDS .. "Button1.wav"
}

audio.extra = {
  warrior = {
    victory = SOUNDS .. "QualeWarriorVictory.wav"
  }
}

audio.menu = {
  main_menu_1 = MENU .. "Menu1.mp3"
}

audio.popups = {
  advisor = SOUNDS .. "PopupAdvisor.wav",
  console = SOUNDS .. "PopupConsole.wav",
  info = SOUNDS .. "PopupInfo.wav"
}

return audio
