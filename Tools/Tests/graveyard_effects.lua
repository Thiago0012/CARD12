-- Test-only fixture: uses the shipped card records, scripts and native filters.
-- Directly invokes Timaeus' registered ATK operation with an effect relation;
-- this tests resolution/counting, not UI input or attack declaration timing.
local function equal(actual,expected,label)
    assert(actual==expected,label..": expected "..expected..", got "..actual)
end
Debug.ReloadFieldBegin(DUEL_MODE_MR5)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local timaeus=Debug.AddCard(85899505,0,0,LOCATION_MZONE,0,POS_FACEUP_ATTACK)
-- Secrets of Dark Magic, Soul Servant, Feast of the Wild LV5.
Debug.AddCard(59514116,0,0,LOCATION_GRAVE,0,POS_FACEUP_ATTACK)
Debug.AddCard(23020408,0,0,LOCATION_REMOVED,0,POS_FACEUP_ATTACK)
Debug.AddCard(55416843,1,1,LOCATION_GRAVE,0,POS_FACEUP_ATTACK)
-- Monsters, a Trap and a Pendulum Monster are NOT Spells in these zones.
Debug.AddCard(38033121,0,0,LOCATION_GRAVE,0,POS_FACEUP_ATTACK)
Debug.AddCard(46986414,1,1,LOCATION_GRAVE,0,POS_FACEUP_ATTACK)
Debug.AddCard(44095762,0,0,LOCATION_GRAVE,0,POS_FACEUP_ATTACK)
Debug.AddCard(15146890,1,1,LOCATION_REMOVED,0,POS_FACEUP_ATTACK)
-- Even a known Spell does not count when banished facedown.
Debug.AddCard(5318639,0,0,LOCATION_REMOVED,0,POS_FACEDOWN_DEFENSE)
Debug.ReloadFieldEnd()

local zones=LOCATION_GRAVE|LOCATION_REMOVED
local spell=aux.FaceupFilter(Card.IsSpell)
equal(Duel.GetMatchingGroupCount(spell,0,zones,zones,nil),3,"Only three faceup Spells")
equal(Duel.GetMatchingGroupCount(spell,1,zones,zones,nil),3,"Both player perspectives")
equal(Duel.GetMatchingGroupCount(Card.IsMonster,0,zones,zones,nil),3,"Monsters including Pendulum")
equal(Duel.GetMatchingGroupCount(Card.IsTrap,0,zones,zones,nil),1,"Trap filter")
equal(Duel.GetMatchingGroupCount(aux.FaceupFilter(Card.IsCode,23020408),0,zones,zones,nil),1,"Exact card identity")
equal(timaeus:GetAttack(),2800,"Printed ATK")
local trigger
for _,effect in ipairs({timaeus:GetOwnEffects()}) do
    if effect:GetCode()==EVENT_PRE_DAMAGE_CALCULATE then trigger=effect break end
end
assert(trigger,"Timaeus damage calculation effect must be registered")
timaeus:CreateEffectRelation(trigger)
assert(trigger:GetTarget()(trigger,0,nil,nil,nil,nil,nil,nil,0),"Effect must be legal with Spells present")
trigger:GetOperation()(trigger,0)
equal(timaeus:GetAttack(),3100,"First resolution: 2800 + 3 x 100")
-- A later legal activation adds another bonus; this is not a continuous total.
trigger:GetOperation()(trigger,0)
equal(timaeus:GetAttack(),3400,"Existing ATK gain persists between resolutions")
timaeus:ReleaseEffectRelation(trigger)
trigger:GetOperation()(trigger,0)
equal(timaeus:GetAttack(),3400,"No gain after losing effect relation")
-- A fourth Spell on the opponent's banished pile must contribute too.
Debug.AddCard(5318639,1,1,LOCATION_REMOVED,0,POS_FACEUP_ATTACK)
equal(Duel.GetMatchingGroupCount(spell,0,zones,zones,nil),4,"Opponent banishment contributes")
timaeus:CreateEffectRelation(trigger)
trigger:GetOperation()(trigger,0)
equal(timaeus:GetAttack(),3800,"Resolution reads the current four Spells")
-- Simulate a standard leave-field reset, without involving any UI logic.
timaeus:ResetEffect(RESET_EVENT|RESETS_STANDARD,RESET_EVENT)
equal(timaeus:GetAttack(),2800,"Standard reset removes the accumulated bonus")

Debug.ReloadFieldBegin(DUEL_MODE_MR5)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local empty=Debug.AddCard(85899505,0,0,LOCATION_MZONE,0,POS_FACEUP_ATTACK)
Debug.AddCard(38033121,0,0,LOCATION_GRAVE,0,POS_FACEUP_ATTACK)
Debug.AddCard(44095762,1,1,LOCATION_REMOVED,0,POS_FACEUP_ATTACK)
Debug.AddCard(5318639,1,1,LOCATION_REMOVED,0,POS_FACEDOWN_DEFENSE)
Debug.ReloadFieldEnd()
local emptyTrigger
for _,effect in ipairs({empty:GetOwnEffects()}) do
    if effect:GetCode()==EVENT_PRE_DAMAGE_CALCULATE then emptyTrigger=effect break end
end
assert(emptyTrigger,"Fresh effect missing")
equal(Duel.GetMatchingGroupCount(spell,0,zones,zones,nil),0,"No eligible Spells")
assert(not emptyTrigger:GetTarget()(emptyTrigger,0,nil,nil,nil,nil,nil,nil,0),"No activation without Spells")
empty:CreateEffectRelation(emptyTrigger)
emptyTrigger:GetOperation()(emptyTrigger,0)
equal(empty:GetAttack(),2800,"No gain from Monsters, Traps or facedown banished Spells")
Debug.Message("PASS: graveyard effect regressions")
