-- Golden age settings for the standalone ruleset.
-- Duration is in turns; shields/commerce are added to each tile's yield during a golden age.
local golden_age = {
  duration = 20,
  shields = 1,
  commerce = 1,
}

return function(ruleset)
  ruleset.rules.goldenAgeDuration = golden_age.duration
  ruleset.rules.goldenAgeBonuses = {
    shields = golden_age.shields,
    commerce = golden_age.commerce,
  }
  return ruleset
end
