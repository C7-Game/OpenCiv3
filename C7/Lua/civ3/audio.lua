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
