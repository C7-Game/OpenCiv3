-- Base paths
local SOUNDS = "Sounds/"
local MENU = SOUNDS .. "Menu/"

-- Audio definitions
local audio = {}

audio.menu = {
  main_menu_1 = MENU .. "Menu1.mp3"
}

audio.buttons = {
  button_1 = SOUNDS .. "Button1.wav"
}

audio.popups = {
  advisor = SOUNDS .. "PopupAdvisor.wav",
  console = SOUNDS .. "PopupConsole.wav",
  info = SOUNDS .. "PopupInfo.wav"
}

return audio
