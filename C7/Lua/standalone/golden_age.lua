-- Golden age settings for the standalone ruleset.
-- Duration is in turns; shields/commerce are added to each tile's yield during a golden age.
local golden_age = {
  duration = 20,
  shields = 1,
  commerce = 1,
}

return function(ruleset)
  -- Clamped so a negative value can't be loaded; 0 disables golden ages
  ruleset.rules.goldenAgeDuration = math.max(0, golden_age.duration)
  ruleset.rules.goldenAgeBonuses = {
    shields = golden_age.shields,
    commerce = golden_age.commerce,
  }
  return ruleset
end
