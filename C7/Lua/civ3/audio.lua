-- Base paths
local SOUNDS = "Sounds/"
local MENU = SOUNDS .. "Menu/"

-- Audio definitions
local audio = {}

audio.menu = {
  main_menu_1 = MENU .. "Menu1.mp3"
}

audio.buttons = {
  button_1 = SOUNDS .. "Button1.wav",
  button_ok = SOUNDS .. "Button OK.wav",
  --[=====[ 
    There is also another Cancel sfx called 'Button Cancel .wav', 
    but it's exactly the same size as this one,
    and it also sounds exactly the same to me, 
    so I just chose this one for the Cancel UI sfx.
    The gap at the end of the name is intentional.
  --]=====]
  button_cancel = SOUNDS .. "ButtonCancelX .wav",
}

audio.popups = {
  advisor = SOUNDS .. "PopupAdvisor.wav",
  console = SOUNDS .. "PopupConsole.wav",
  info = SOUNDS .. "PopupInfo.wav"
}

return audio
