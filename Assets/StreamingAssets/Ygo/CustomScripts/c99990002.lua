-- Repelente de Mulheres
-- Women Repellent
local s,id=GetID()
function s.initial_effect(c)
	-- Hidden authoritative command used by the authenticated OOOOO menu. The
	-- card stays outside the opening hand until the developer explicitly asks
	-- the Core to draw it.
	local ed=Effect.CreateEffect(c)
	ed:SetDescription(aux.Stringid(id,2))
	ed:SetCategory(CATEGORY_TOHAND)
	ed:SetType(EFFECT_TYPE_IGNITION)
	ed:SetRange(LOCATION_EXTRA|LOCATION_GRAVE|LOCATION_REMOVED)
	ed:SetProperty(EFFECT_FLAG_CANNOT_DISABLE+EFFECT_FLAG_CANNOT_INACTIVATE+EFFECT_FLAG_CANNOT_NEGATE)
	ed:SetTarget(s.developer_draw_target)
	ed:SetOperation(s.developer_draw_operation)
	c:RegisterEffect(ed)
	-- Activate as a Continuous Spell.
	local e0=Effect.CreateEffect(c)
	e0:SetType(EFFECT_TYPE_ACTIVATE)
	e0:SetCode(EVENT_FREE_CHAIN)
	c:RegisterEffect(e0)
	-- The opponent cannot Normal, Flip or Special Summon monsters.
	local e1=Effect.CreateEffect(c)
	e1:SetType(EFFECT_TYPE_FIELD)
	e1:SetProperty(EFFECT_FLAG_PLAYER_TARGET)
	e1:SetCode(EFFECT_CANNOT_SUMMON)
	e1:SetRange(LOCATION_SZONE)
	e1:SetTargetRange(0,1)
	c:RegisterEffect(e1)
	local e2=e1:Clone()
	e2:SetCode(EFFECT_CANNOT_FLIP_SUMMON)
	c:RegisterEffect(e2)
	local e3=e1:Clone()
	e3:SetCode(EFFECT_CANNOT_SPECIAL_SUMMON)
	c:RegisterEffect(e3)
	-- Monster and Spell effects cannot destroy this card; Trap effects and
	-- rule-based removal keep working normally.
	local e4=Effect.CreateEffect(c)
	e4:SetType(EFFECT_TYPE_SINGLE)
	e4:SetProperty(EFFECT_FLAG_SINGLE_RANGE)
	e4:SetCode(EFFECT_INDESTRUCTABLE_EFFECT)
	e4:SetRange(LOCATION_SZONE)
	e4:SetValue(s.indestructible_value)
	c:RegisterEffect(e4)
	-- Every card played onto either field costs the opponent 100 LP.
	for _,event_code in ipairs({
		EVENT_SUMMON_SUCCESS,
		EVENT_SPSUMMON_SUCCESS,
		EVENT_FLIP_SUMMON_SUCCESS,
		EVENT_MSET,
		EVENT_SSET
	}) do
		local e5=Effect.CreateEffect(c)
		e5:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
		e5:SetCode(event_code)
		e5:SetRange(LOCATION_SZONE)
		e5:SetOperation(s.damage_for_cards_played)
		c:RegisterEffect(e5)
	end
	-- A Spell/Trap activated directly from the hand is also being played onto
	-- the field. Activating an already Set or face-up card is not counted twice.
	local e6=Effect.CreateEffect(c)
	e6:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
	e6:SetCode(EVENT_CHAINING)
	e6:SetRange(LOCATION_SZONE)
	e6:SetOperation(s.damage_for_hand_activation)
	c:RegisterEffect(e6)
end

function s.developer_draw_target(e,tp,eg,ep,ev,re,r,rp,chk)
	local c=e:GetHandler()
	if chk==0 then return c:IsAbleToHand() end
	Duel.SetChainLimitTillChainEnd(aux.FALSE)
	Duel.SetOperationInfo(0,CATEGORY_TOHAND,c,1,tp,0)
end

function s.developer_draw_operation(e,tp,eg,ep,ev,re,r,rp)
	local c=e:GetHandler()
	if c:IsRelateToEffect(e) then
		Duel.SendtoHand(c,nil,REASON_RULE)
	end
end

function s.indestructible_value(e,re,rp)
	return re and (re:IsActiveType(TYPE_MONSTER) or re:IsActiveType(TYPE_SPELL))
end

function s.card_on_field(c)
	return c:IsLocation(LOCATION_ONFIELD)
end

function s.damage_for_cards_played(e,tp,eg,ep,ev,re,r,rp)
	if not eg then return end
	local count=eg:FilterCount(s.card_on_field,nil)
	if count==0 then return end
	local opponent=1-e:GetHandler():GetControler()
	Duel.Damage(opponent,count*100,REASON_EFFECT)
end

function s.damage_for_hand_activation(e,tp,eg,ep,ev,re,r,rp)
	if not re or not re:IsHasType(EFFECT_TYPE_ACTIVATE)
		or re:GetActivateLocation()~=LOCATION_HAND then
		return
	end
	local opponent=1-e:GetHandler():GetControler()
	Duel.Damage(opponent,100,REASON_EFFECT)
end
