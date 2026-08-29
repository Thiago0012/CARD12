-- Mixael, o Olhar Maldito
-- Mixael, the Cursed Gaze
local s,id=GetID()
function s.initial_effect(c)
	-- The game injects this card only for an authenticated development account.
	-- Online injection happens in the host's authoritative Core; the Core still
	-- asks the player to select a legal empty Monster Zone in every mode.
	local e0=Effect.CreateEffect(c)
	e0:SetType(EFFECT_TYPE_FIELD)
	e0:SetCode(EFFECT_SPSUMMON_PROC)
	e0:SetProperty(EFFECT_FLAG_UNCOPYABLE+EFFECT_FLAG_CANNOT_DISABLE)
	e0:SetRange(LOCATION_EXTRA)
	e0:SetCondition(s.development_summon_condition)
	-- The MMM procedure has no materials, but it is still this card's
	-- developer Synchro Summon.  Marking the summon type in the Core makes the
	-- mandatory on-Synchro-Summon trigger below resolve in local and online
	-- authoritative duels instead of treating MMM as a generic Special Summon.
	e0:SetValue(SUMMON_TYPE_SYNCHRO)
	c:RegisterEffect(e0)
	Synchro.AddProcedure(c,nil,3,3,Synchro.NonTuner(nil),3,3,nil,nil,nil,nil,s.material_requirement)
	c:EnableReviveLimit()
	-- Banish all other cards on the field, in the hands and in the GYs when Synchro Summoned.
	local e1=Effect.CreateEffect(c)
	e1:SetCategory(CATEGORY_REMOVE)
	e1:SetType(EFFECT_TYPE_SINGLE+EFFECT_TYPE_TRIGGER_F)
	e1:SetCode(EVENT_SPSUMMON_SUCCESS)
	e1:SetProperty(EFFECT_FLAG_CANNOT_DISABLE+EFFECT_FLAG_CANNOT_INACTIVATE+EFFECT_FLAG_CANNOT_NEGATE)
	e1:SetCondition(s.banish_condition)
	e1:SetTarget(s.banish_target)
	e1:SetOperation(s.banish_operation)
	c:RegisterEffect(e1)
	-- Can attack directly.
	local e2=Effect.CreateEffect(c)
	e2:SetType(EFFECT_TYPE_SINGLE)
	e2:SetCode(EFFECT_DIRECT_ATTACK)
	c:RegisterEffect(e2)
	-- A direct attack wins the Duel.
	local e3=Effect.CreateEffect(c)
	e3:SetType(EFFECT_TYPE_SINGLE+EFFECT_TYPE_TRIGGER_F)
	e3:SetCode(EVENT_ATTACK_ANNOUNCE)
	e3:SetProperty(EFFECT_FLAG_CANNOT_DISABLE+EFFECT_FLAG_CANNOT_INACTIVATE+EFFECT_FLAG_CANNOT_NEGATE)
	e3:SetCondition(s.direct_attack_condition)
	e3:SetTarget(s.direct_attack_target)
	e3:SetOperation(s.direct_attack_operation)
	c:RegisterEffect(e3)
	-- Once per turn, banish all cards the opponent controls.
	local e4=Effect.CreateEffect(c)
	e4:SetDescription(aux.Stringid(id,1))
	e4:SetCategory(CATEGORY_REMOVE)
	e4:SetType(EFFECT_TYPE_IGNITION)
	e4:SetRange(LOCATION_MZONE)
	e4:SetCountLimit(1,id)
	e4:SetProperty(EFFECT_FLAG_CANNOT_DISABLE+EFFECT_FLAG_CANNOT_INACTIVATE+EFFECT_FLAG_CANNOT_NEGATE)
	e4:SetTarget(s.opponent_banish_target)
	e4:SetOperation(s.opponent_banish_operation)
	c:RegisterEffect(e4)
	-- Unaffected by other cards' effects.
	local e5=Effect.CreateEffect(c)
	e5:SetType(EFFECT_TYPE_SINGLE)
	e5:SetCode(EFFECT_IMMUNE_EFFECT)
	e5:SetProperty(EFFECT_FLAG_SINGLE_RANGE)
	e5:SetRange(LOCATION_MZONE)
	e5:SetValue(s.immune_filter)
	c:RegisterEffect(e5)
end

function s.development_summon_condition(e,c)
	if c==nil then return true end
	local tp=c:GetControler()
	return Duel.GetLocationCountFromEx(tp,tp,nil,c)>0
end

function s.material_requirement(group,sc,tp)
	return group:IsExists(s.non_tuner_race,1,nil,RACE_BEAST,sc,tp) and
		group:IsExists(s.non_tuner_race,1,nil,RACE_FIEND,sc,tp) and
		group:IsExists(s.non_tuner_race,1,nil,RACE_FAIRY,sc,tp)
end

function s.non_tuner_race(c,race,sc,tp)
	return c:IsNotTuner(sc,tp) and c:IsRace(race)
end

function s.banish_condition(e,tp,eg,ep,ev,re,r,rp)
	return e:GetHandler():IsSummonType(SUMMON_TYPE_SYNCHRO)
end

function s.banish_target(e,tp,eg,ep,ev,re,r,rp,chk)
	if chk==0 then return true end
	Duel.SetChainLimitTillChainEnd(aux.FALSE)
	Duel.SetOperationInfo(0,CATEGORY_REMOVE,nil,0,PLAYER_ALL,LOCATION_ONFIELD|LOCATION_HAND|LOCATION_GRAVE)
end

function s.banish_filter(c,handler)
	return c~=handler and c:IsAbleToRemove()
end

function s.banish_operation(e,tp,eg,ep,ev,re,r,rp)
	local c=e:GetHandler()
	local g=Duel.GetMatchingGroup(s.banish_filter,tp,LOCATION_ONFIELD|LOCATION_HAND|LOCATION_GRAVE,LOCATION_ONFIELD|LOCATION_HAND|LOCATION_GRAVE,c,c)
	if #g>0 then
		Duel.Remove(g,POS_FACEUP,REASON_EFFECT)
	end
end

function s.direct_attack_condition(e,tp,eg,ep,ev,re,r,rp)
	return Duel.GetAttacker()==e:GetHandler() and Duel.GetAttackTarget()==nil
end

function s.direct_attack_target(e,tp,eg,ep,ev,re,r,rp,chk)
	if chk==0 then return true end
	Duel.SetChainLimitTillChainEnd(aux.FALSE)
end

function s.direct_attack_operation(e,tp,eg,ep,ev,re,r,rp)
	Duel.Win(tp,WIN_REASON_CREATORGOD)
end

function s.opponent_banish_target(e,tp,eg,ep,ev,re,r,rp,chk)
	if chk==0 then return Duel.IsExistingMatchingCard(Card.IsAbleToRemove,tp,0,LOCATION_ONFIELD,1,nil) end
	Duel.SetChainLimitTillChainEnd(aux.FALSE)
	Duel.SetOperationInfo(0,CATEGORY_REMOVE,nil,0,1-tp,LOCATION_ONFIELD)
end

function s.opponent_banish_operation(e,tp,eg,ep,ev,re,r,rp)
	local g=Duel.GetMatchingGroup(Card.IsAbleToRemove,tp,0,LOCATION_ONFIELD,nil)
	if #g>0 then
		Duel.Remove(g,POS_FACEUP,REASON_EFFECT)
	end
end

function s.immune_filter(e,re)
	return re:GetOwner()~=e:GetOwner()
end
