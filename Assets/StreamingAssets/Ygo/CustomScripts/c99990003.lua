-- Zona de Probabilidade Tendendo a Zero - Azar Iminente
-- Probability Zone Approaching Zero - Imminent Misfortune
local s,id=GetID()
function s.initial_effect(c)
	-- Activate, then place this Field Spell in the opponent's Field Zone.
	local e0=Effect.CreateEffect(c)
	e0:SetType(EFFECT_TYPE_ACTIVATE)
	e0:SetCode(EVENT_FREE_CHAIN)
	e0:SetProperty(EFFECT_FLAG_CANNOT_DISABLE+EFFECT_FLAG_CANNOT_INACTIVATE+EFFECT_FLAG_CANNOT_NEGATE)
	e0:SetOperation(s.activate)
	c:RegisterEffect(e0)
	-- The misfortune effect cannot be disabled while this card is in a Field
	-- Zone. Its internal probability is intentionally not presented to players.
	local e1=Effect.CreateEffect(c)
	e1:SetType(EFFECT_TYPE_SINGLE)
	e1:SetProperty(EFFECT_FLAG_SINGLE_RANGE+EFFECT_FLAG_CANNOT_DISABLE)
	e1:SetCode(EFFECT_CANNOT_DISABLE)
	e1:SetRange(LOCATION_FZONE)
	c:RegisterEffect(e1)
	-- Resolve the chance immediately after a monster is Summoned or a card is
	-- Set, before that newly played card can start its own trigger chain.
	for _,event_code in ipairs({
		EVENT_SUMMON_SUCCESS,
		EVENT_SPSUMMON_SUCCESS,
		EVENT_FLIP_SUMMON_SUCCESS,
		EVENT_MSET,
		EVENT_SSET
	}) do
		local e2=Effect.CreateEffect(c)
		e2:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
		e2:SetProperty(EFFECT_FLAG_CANNOT_DISABLE)
		e2:SetCode(event_code)
		e2:SetRange(LOCATION_FZONE)
		e2:SetOperation(s.check_cards_played)
		c:RegisterEffect(e2)
	end
	-- Spell/Trap activations made directly from the hand are checked before
	-- resolving. A Set card that already survived its placement is not checked
	-- again when it is activated later.
	local e3=Effect.CreateEffect(c)
	e3:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
	e3:SetProperty(EFFECT_FLAG_CANNOT_DISABLE)
	e3:SetCode(EVENT_CHAINING)
	e3:SetRange(LOCATION_FZONE)
	e3:SetOperation(s.check_hand_activation)
	c:RegisterEffect(e3)
end

function s.activate(e,tp,eg,ep,ev,re,r,rp)
	local c=e:GetHandler()
	if not c:IsRelateToEffect(e) then return end
	local opponent=1-tp
	local occupied=Duel.GetFieldCard(opponent,LOCATION_FZONE,0)
	if occupied then
		Duel.SendtoGrave(occupied,REASON_RULE)
		Duel.BreakEffect()
	end
	Duel.MoveToField(c,tp,opponent,LOCATION_FZONE,POS_FACEUP,true)
end

function s.misfortune_occurs()
	-- The Core owns the hidden random stream, so host and replica always receive
	-- the same result without exposing probability details in presentation.
	return Duel.GetRandomNumber(1,20)<=13
end

function s.register_survived_set(c)
	c:RegisterFlagEffect(id,RESET_EVENT|RESETS_STANDARD,0,1)
end

function s.consume_survived_set(c)
	if c:GetFlagEffect(id)==0 then return false end
	c:ResetFlagEffect(id)
	return true
end

function s.prevent_card_triggers(source,target)
	local lock=Effect.CreateEffect(source)
	lock:SetType(EFFECT_TYPE_SINGLE)
	lock:SetProperty(EFFECT_FLAG_CANNOT_DISABLE)
	lock:SetCode(EFFECT_CANNOT_TRIGGER)
	lock:SetReset(RESET_PHASE|PHASE_END)
	target:RegisterEffect(lock,true)
end

function s.resolve_played_card(source,target)
	if s.consume_survived_set(target) then return end
	if not s.misfortune_occurs() then
		if target:IsFacedown() then s.register_survived_set(target) end
		return
	end
	s.prevent_card_triggers(source,target)
	Duel.Destroy(target,REASON_EFFECT)
end

function s.check_cards_played(e,tp,eg,ep,ev,re,r,rp)
	if not eg then return end
	local controller=e:GetHandler():GetControler()
	local tc=eg:GetFirst()
	while tc do
		if tc:IsControler(controller) and tc:IsLocation(LOCATION_ONFIELD) then
			s.resolve_played_card(e:GetHandler(),tc)
		end
		tc=eg:GetNext()
	end
end

function s.check_hand_activation(e,tp,eg,ep,ev,re,r,rp)
	if not re or not re:IsHasType(EFFECT_TYPE_ACTIVATE) then return end
	local c=e:GetHandler()
	local rc=re:GetHandler()
	if rc==c or rp~=c:GetControler() then return end
	if s.consume_survived_set(rc) then return end
	if re:GetActivateLocation()~=LOCATION_HAND or not s.misfortune_occurs() then
		return
	end
	s.prevent_card_triggers(c,rc)
	Duel.NegateActivation(ev)
	if rc:IsRelateToEffect(re) then
		Duel.Destroy(rc,REASON_EFFECT)
	end
end
