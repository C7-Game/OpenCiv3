return {
  buildings = require "behaviors.buildings",

  --[[
	  terraforms module provides rules for worker actions (e.g., clearing forest or mining),
	  while terrain_improvements describes improvements placed on a map (e.g., mine, irrigation, railroad)
  --]]
  terraforms = require "behaviors.terraforms",
  terrain_improvements = require "behaviors.terrain_improvements",
  inflows = require "behaviors.inflows",
  gameplay = require "behaviors.gameplay",
  goody_huts = require "behaviors.goody_huts",
}
