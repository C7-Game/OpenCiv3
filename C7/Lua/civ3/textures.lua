-- Base paths
local ROOT = "Art/"
local ADVISORS = "Art/Advisors/"

local BUTTONS = "Art/buttonsFINAL.pcx"
local CHECKBOXES = "Art/3checkboxes-USE.pcx"
local EXIT_BOX = "Art/exitBox-backgroundStates.pcx"
local INTERFACE = "Art/interface/"
local X_O = "Art/X-o_ALLstates-sprite.pcx"
local SCIENCE_NAV = "Art/Tech Chooser/scienceNAV.pcx"
local TILEINFO = "Art/TileInfo.pcx"

local CITY_SCREEN = "Art/city screen/"
local CITY_BUTTONS = "Art/city screen/cityMgmtButtons.pcx"
local CITY_PRODUCTION = "Art/city screen/ProdButton.pcx"
local CITY_SCREEN_ICONS = "Art/city screen/CityIcons.pcx"

local CITY_ICONS = "Art/Cities/city icons.pcx"

local CREDITS = "Art/Credits/"
local PALACE = "Art/PalaceView/"
local SPACE_RACE = "Art/SpaceShip/"

local POPUP_BORDERS = "Art/popupborders.pcx"

local CURSORS = "Art/Cursor.pcx"
local SCROLL = "Art/SCROLL.PCX"

-- Texture definitions
local textures = {}

textures.advisors = {
  dialog_box = ADVISORS .. "dialogbox.pcx",
  science = {
    background = {
      ancient = ADVISORS .. "science_ancient.pcx",
      middle = ADVISORS .. "science_middle.pcx",
      industrial = ADVISORS .. "science_industrial_new.pcx",
      modern = ADVISORS .. "science_modern.pcx",
    },
    navigation = {
      button = {
        normal = {
          path = SCIENCE_NAV,
          crop_region = { 0, 1, 129, 33 },
        },
        hover = {
          path = SCIENCE_NAV,
          crop_region = { 0, 35, 129, 33 },
        },
        pressed = {
          path = SCIENCE_NAV,
          crop_region = { 0, 69, 129, 33 },
        },
      },
      arrow_previous = {
        path = SCIENCE_NAV,
        crop_region = { 0, 103, 44, 9 },
      },
      arrow_next = {
        path = SCIENCE_NAV,
        crop_region = { 46, 103, 44, 9 },
      },
    },
  },
  military = {
    background = ADVISORS .. "military.pcx",
  },
  domestic = {
    background = {
      path = ADVISORS .. "domestic.pcx",
      shadows = false,
    },
    button = {
      normal = {
        path = ADVISORS .. "domesticBUTTON.pcx",
        crop_region = { 1, 1, 145, 24 },
      },
      hover = {
        path = ADVISORS .. "domesticBUTTON.pcx",
        crop_region = { 1, 26, 145, 24 },
      },
      pressed = {
        path = ADVISORS .. "domesticBUTTON.pcx",
        crop_region = { 1, 52, 145, 24 },
      },
    },
    minus = {
      normal = {
        path = ADVISORS .. "domestic_plusminus.pcx",
        crop_region = { 0, 0, 13, 9 },
        shadows = false,
      },
      hover = {
        path = ADVISORS .. "domestic_plusminus.pcx",
        crop_region = { 12, 0, 13, 9 },
        shadows = false,
      },
      pressed = {
        path = ADVISORS .. "domestic_plusminus.pcx",
        crop_region = { 24, 0, 13, 9 },
        shadows = false,
      },
    },
    plus = {
      normal = {
        path = ADVISORS .. "domestic_plusminus.pcx",
        crop_region = { 0, 8, 13, 14 },
        shadows = false,
      },
      hover = {
        path = ADVISORS .. "domestic_plusminus.pcx",
        crop_region = { 12, 8, 13, 14 },
        shadows = false,
      },
      pressed = {
        path = ADVISORS .. "domestic_plusminus.pcx",
        crop_region = { 24, 8, 13, 14 },
        shadows = false,
      },
    },
  },
  trade = {
    background = ADVISORS .. "trade.pcx",
  },
  foreign = {
    background = ADVISORS .. "foreign.pcx",
    navigation = {
      treaties = {
        path = ADVISORS .. "foreignTAB.pcx",
        crop_region = { 1, 1, 223, 236 },
      },
      trades = {
        path = ADVISORS .. "foreignTAB.pcx",
        crop_region = { 225, 1, 223, 236 },
      },
      details = {
        path = ADVISORS .. "foreignTAB.pcx",
        crop_region = { 449, 1, 223, 236 },
      }
    }
  },
  culture = {
    background = ADVISORS .. "culture.pcx",
  }
}

textures.screens = {
  wonders = {
    background = ADVISORS .. "wonders_background.pcx",
  },
  standing = {
    victory_status = {
      background = ROOT .. "histograph-win5_final.pcx",
    },
    histogram = {
      background = ROOT .. "histograph-top5_final.pcx",
    }
  },
  space_race = {
    background = SPACE_RACE .. "SHIPbackground.pcx",
  },
  palace = {
    background = PALACE .. "bkgr.pcx",
  },
  demographics = {
    background = ADVISORS .. "demographics.pcx",
  },

}

textures.ui = {
  button = {
    inactive = {
      path = BUTTONS,
      crop_region = { 1, 1, 20, 20 },
      shadows = false,
    },
    hover = {
      path = BUTTONS,
      crop_region = { 22, 1, 20, 20 },
      shadows = false,
    },
    pressed = {
      path = BUTTONS,
      crop_region = { 43, 1, 20, 20 },
      shadows = false,
    },
  },
  checkbox = {
    inactive = {
      path = CHECKBOXES,
      crop_region = { 1, 1, 15, 15 },
      shadows = false,
    },
    hover = {
      path = CHECKBOXES,
      crop_region = { 17, 1, 15, 15 },
      shadows = false,
    },
    pressed = {
      path = CHECKBOXES,
      crop_region = { 33, 1, 15, 15 },
      shadows = false,
    },
  },
  confirm = {
    normal = {
      path = X_O,
      crop_region = { 1, 1, 19, 19 },
    },
    hover = {
      path = X_O,
      crop_region = { 37, 1, 19, 19 },
    },
    pressed = {
      path = X_O,
      crop_region = { 73, 1, 19, 19 },
    },
  },
  cancel = {
    normal = {
      path = X_O,
      crop_region = { 21, 1, 15, 19 },
    },
    hover = {
      path = X_O,
      crop_region = { 57, 1, 15, 19 },
    },
    pressed = {
      path = X_O,
      crop_region = { 93, 1, 15, 19 },
    },
  },
  console = {
    normal = {
      path = INTERFACE .. "consoleButtons.pcx",
      crop_region = { 1, 1, 16, 16 },
    },
    hover = {
      path = INTERFACE .. "consoleButtons.pcx",
      crop_region = { 17, 1, 16, 16 },
    },
    pressed = {
      path = INTERFACE .. "consoleButtons.pcx",
      crop_region = { 33, 1, 16, 16 },
    },
  },
  exit = {
    normal = {
      path = EXIT_BOX,
      crop_region = { 0, 0, 72, 48 },
    },
    hover = {
      path = EXIT_BOX,
      crop_region = { 72, 0, 72, 48 },
    },
    pressed = {
      path = EXIT_BOX,
      crop_region = { 144, 0, 72, 48 },
    },
  },
  cursor = {
      bombard_deny = {
          path = CURSORS,
          crop_region = { 166, 1, 32, 32 },
      },
      bombard = {
          path = CURSORS,
          crop_region = { 199, 1, 32, 32 },
      }
  },
  scrollbar = {
    grabber = {
      large = {
        path = SCROLL,
        crop_region = { 46, 139, 20, 20 },
        shadows = false,
      },
      small = {
        path = SCROLL,
        crop_region = { 51, 254, 10, 10 },
        shadows = false,
      },
    },
    scroll = {
      vertical = {
        large = {
          path = SCROLL,
          crop_region = { 9, 139, 20, 20 },
          shadows = false,
        },
        small = {
          path = SCROLL,
          crop_region = { 14, 249, 10, 20 },
          shadows = false,
        },
      },
      horizontal = {
        large = {
          path = SCROLL,
          crop_region = { 9, 380, 20, 20 },
          shadows = false,
        },
        small = {
          path = SCROLL,
          crop_region = { 119, 385, 20, 10 },
          shadows = false,
        },
      },
    },
    decrement = {
      vertical = {
        large = {
          normal = {
            path = SCROLL,
            crop_region = { 2, 25, 20, 20 },
            shadows = false,
          },
          hover = {
            path = SCROLL,
            crop_region = { 25, 25, 20, 20 },
            shadows = false,
          },
          pressed = {
            path = SCROLL,
            crop_region = { 48, 25, 20, 20 },
            shadows = false,
          },
        },
        small = {
          normal = {
            path = SCROLL,
            crop_region = { 117, 15, 10, 10 },
            shadows = false,
          },
          hover = {
            path = SCROLL,
            crop_region = { 130, 15, 10, 10 },
            shadows = false,
          },
          pressed = {
            path = SCROLL,
            crop_region = { 143, 15, 10, 10 },
            shadows = false,
          },
        },
      },
      horizontal = {
        large = {
          normal = {
            path = SCROLL,
            crop_region = { 94, 2, 20, 20 },
            shadows = false,
          },
          hover = {
            path = SCROLL,
            crop_region = { 94, 25, 20, 20 },
            shadows = false,
          },
          pressed = {
            path = SCROLL,
            crop_region = { 94, 48, 20, 20 },
            shadows = false,
          },
        },
        small = {
          normal = {
            path = SCROLL,
            crop_region = { 169, 2, 10, 10 },
            shadows = false,
          },
          hover = {
            path = SCROLL,
            crop_region = { 169, 15, 10, 10 },
            shadows = false,
          },
          pressed = {
            path = SCROLL,
            crop_region = { 169, 28, 10, 10 },
            shadows = false,
          },
        },
      },
    },
    increment = {
      vertical = {
        large = {
          normal = {
            path = SCROLL,
            crop_region = { 2, 2, 20, 20 },
            shadows = false,
          },
          hover = {
            path = SCROLL,
            crop_region = { 25, 2, 20, 20 },
            shadows = false,
          },
          pressed = {
            path = SCROLL,
            crop_region = { 48, 2, 20, 20 },
            shadows = false,
          },
        },
        small = {
          normal = {
           path = SCROLL,
           crop_region = { 117, 2, 10, 10 },
           shadows = false,
          },
          hover = {
            path = SCROLL,
            crop_region = { 130, 2, 10, 10 },
            shadows = false,
          },
          pressed = {
            path = SCROLL,
            crop_region = { 143, 2, 10, 10 },
            shadows = false,
          },
        },
      },
      horizontal = {
        large = {
          normal = {
            path = SCROLL,
            crop_region = { 71, 2, 20, 20 },
            shadows = false,
          },
          hover = {
            path = SCROLL,
            crop_region = { 71, 25, 20, 20 },
            shadows = false,
          },
          pressed = {
            path = SCROLL,
            crop_region = { 71, 48, 20, 20 },
            shadows = false,
          },
        },
        small = {
          normal = {
            path = SCROLL,
            crop_region = { 156, 2, 10, 10 },
            shadows = false,
          },
          hover = {
            path = SCROLL,
            crop_region = { 156, 15, 10, 10 },
            shadows = false,
          },
          pressed = {
            path = SCROLL,
            crop_region = { 156, 28, 10, 10 },
            shadows = false,
          },
        },
      },
    },
  },
}

textures.terrain = require "textures.terrain"

textures.resources = require "textures.resources"

textures.credits = {
  background = CREDITS .. "credits_background.pcx",
}

textures.icons = {
  science = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 34, 2, 30, 30 },
    shadows = false,
  },
  beaker = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 34, 2, 29, 29 },
  },
  good_gold = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 64, 2, 29, 29 },
  },
  commerce = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 67, 1, 21, 30 },
  },
  wasted_gold = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 95, 2, 30, 30 },
  },
  good_shield = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 129, 5, 22, 22 },
  },
  shield = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 133, 1, 16, 30 },
  },
  wasted_shield = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 160, 5, 22, 22 },
  },
  full_food = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 191, 7, 20, 20 },
  },
  food = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 195, 1, 21, 30 },
  },
  eaten_food = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 223, 7, 20, 20 },
  },
  empty_shield = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 253, 5, 22, 22 },
  },
  empty_food = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 284, 7, 20, 20 },
  },
  no_food = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 315, 7, 20, 20 },
  },
  luxury = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 376, 2, 30, 30 },
    shadows = false,
  },
  happy_face = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 373, 2, 29, 29 },
  },
  effect_smiley_face = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 373, 1, 30, 30 },
  },
  effect_shield = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 404, 1, 30, 30 },
  },
  effect_good_gold = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 435, 1, 30, 30 },
  },
  effect_beaker = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 497, 1, 30, 30 },
  },
  effect_wasted_gold = {
    path = CITY_SCREEN_ICONS,
    crop_region = { 528, 1, 30, 30 },
  },
  content_face = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 591, 2, 29, 29 },
  },
  treasury = {
	  path = CITY_SCREEN_ICONS,
	  crop_region = { 746, 2, 29, 29 },
  },
  plus = {
    path = ADVISORS .. "domestic_icons_aux.pcx",
    crop_region = { 75, 1, 22, 22 },
    shadows = false,
  },
  minus = {
    path = ADVISORS .. "domestic_icons_aux.pcx",
    crop_region = { 51, 1, 22, 22 },
    shadows = false,
  },
  maintenance = {
    path = ADVISORS .. "domestic_icons_aux.pcx",
    crop_region = { 1, 1, 19, 34 },
  },
  capital_star = {
    path = CITY_ICONS,
    crop_region = { 20, 1, 18, 18 },
  },
}

textures.city_screen = {
  background = "Art/city screen/background.pcx",
  buttons = {
    close = {
      normal = {
        path = CITY_BUTTONS,
        crop_region = { 155, 1, 38, 48 },
      },
      hover = {
        path = CITY_BUTTONS,
        crop_region = { 155, 50, 38, 48 },
      },
      pressed = {
        path = CITY_BUTTONS,
        crop_region = { 155, 99, 38, 48 },
      },
    },
    previous = {
      normal = {
        path = CITY_BUTTONS,
        crop_region = { 1, 1, 48, 48 },
      },
      hover = {
        path = CITY_BUTTONS,
        crop_region = { 1, 50, 48, 48 },
      },
      pressed = {
        path = CITY_BUTTONS,
        crop_region = { 1, 99, 48, 48 },
      },
    },
    next = {
      normal = {
        path = CITY_BUTTONS,
        crop_region = { 42, 1, 48, 48 },
      },
      hover = {
        path = CITY_BUTTONS,
        crop_region = { 42, 50, 48, 48 },
      },
      pressed = {
        path = CITY_BUTTONS,
        crop_region = { 42, 99, 48, 48 },
      },
    },
    production = {
      normal = {
        path = CITY_PRODUCTION,
        crop_region = { 1, 0, 114, 95 },
      },
      hover = {
        path = CITY_PRODUCTION,
        crop_region = { 116, 0, 115, 95 },
      },
      pressed = {
        path = CITY_PRODUCTION,
        crop_region = { 231, 0, 115, 95 },
      },
    },
  },
  production_queue = CITY_SCREEN .. "ProductionQueueBox.pcx",
}

textures.world_setup = require "textures.world_setup"
textures.player_setup = require "textures.player_setup"

textures.diplomacy = {
  deal = "Art/Diplomacy/counter.pcx",
  offer = "Art/Diplomacy/talk_offer.pcx",
}

textures.upper_left_navigation = {
  menu = {
    path = INTERFACE .. "menuButtons.pcx",
    alpha = INTERFACE .. "menuButtonsAlpha.pcx",
    crop_region = { 0, 1, 35, 29 },
  },
  civilopedia = {
    path = INTERFACE .. "menuButtons.pcx",
    alpha = INTERFACE .. "menuButtonsAlpha.pcx",
    crop_region = { 36, 1, 35, 29 },
  },
  advisor = {
    normal = {
      path = INTERFACE .. "menuButtons.pcx",
      alpha = INTERFACE .. "menuButtonsAlpha.pcx",
      crop_region = { 73, 1, 35, 29 },
    },
    hover = {
      path = INTERFACE .. "menuButtons.pcx",
      alpha = INTERFACE .. "menuButtonsAlpha.pcx",
      alpha_row_offset = 60,
      crop_region = { 73, 61, 35, 29 },
    },
    pressed = {
      path = INTERFACE .. "menuButtons.pcx",
      alpha = INTERFACE .. "menuButtonsAlpha.pcx",
      alpha_row_offset = 120,
      crop_region = { 73, 121, 35, 29 },
    },
  },
}

textures.lower_right_infobox = {
  box = {
    path = INTERFACE .. "box right color.pcx",
    alpha = INTERFACE .. "box right alpha.pcx",
  },
  next_turn = {
    off = {
      path = INTERFACE .. "nextturn states color.pcx",
      alpha = INTERFACE .. "nextturn states alpha.pcx",
      crop_region = { 0, 0, 47, 28 },
    },
    on = {
      path = INTERFACE .. "nextturn states color.pcx",
      alpha = INTERFACE .. "nextturn states alpha.pcx",
      crop_region = { 47, 0, 47, 28 },
    },
    blink = {
      path = INTERFACE .. "nextturn states color.pcx",
      alpha = INTERFACE .. "nextturn states alpha.pcx",
      crop_region = { 94, 0, 47, 28 },
    },
  },
}

textures.lower_left_infobox = {
    box = {
      path = INTERFACE .. "box left color.pcx",
      alpha = INTERFACE .. "box left alpha.pcx",
    },
}

textures.transport_infobox = {
    box = {
      path = INTERFACE .. "box trans color.pcx",
      alpha = INTERFACE .. "box trans alpha.pcx",
    },
}

textures.tileinfo = {
      box = {
        path = TILEINFO
      },
}

textures.popup_background = {
  top_left = {
    path = POPUP_BORDERS,
    crop_region = { 251, 1, 61, 44 },
  },
  top_center = {
    path = POPUP_BORDERS,
    crop_region = { 313, 1, 61, 44 },
  },
  top_right = {
    path = POPUP_BORDERS,
    crop_region = { 375, 1, 61, 44 },
  },
  middle_left = {
    path = POPUP_BORDERS,
    crop_region = { 251, 46, 61, 44 },
  },
  middle_center = {
    path = POPUP_BORDERS,
    crop_region = { 313, 46, 61, 44 },
  },
  middle_right = {
    path = POPUP_BORDERS,
    crop_region = { 375, 46, 61, 44 },
  },
  bottom_left = {
    path = POPUP_BORDERS,
    crop_region = { 251, 91, 61, 44 },
  },
  bottom_center = {
    path = POPUP_BORDERS,
    crop_region = { 313, 91, 61, 44 },
  },
  bottom_right = {
    path = POPUP_BORDERS,
    crop_region = { 375, 91, 61, 44 },
  },
}

textures.animations = {
  cursor = {
    path = "Art/Animations/Cursor/Cursor.flc",
  },
  disorder = {
    path = "Art/Animations/Disorder/DisorderDefault.flc",
  },
}

textures.popheads = require "textures.popheads"
textures.cities = require "textures.cities"
textures.advisor_heads = require "textures.advisor_heads"
textures.ui.unit_control = require "textures.unit_control"
textures.terrain_improvements = require "textures.terrain_improvements"
textures.civ_colors = require "textures.civ_colors"
textures.unit_icons = require "textures.unit_icons"
textures.building_icons = require "textures.building_icons"
textures.tech_icons = require "textures.tech_icons"
textures.tech_boxes = require "textures.tech_boxes"
textures.leader_heads = require "textures.leader_heads"
textures.borders = require "textures.borders"

return textures
