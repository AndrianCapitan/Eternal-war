body-zone-head = head
body-zone-left-eye = left eye
body-zone-right-eye = right eye
body-zone-mouth = mouth
body-zone-chest = chest
body-zone-abdomen = abdomen
body-zone-genitals = genitals
body-zone-left-upper-arm = left upper arm
body-zone-right-upper-arm = right upper arm
body-zone-left-forearm = left forearm
body-zone-right-forearm = right forearm
body-zone-left-hand = left hand
body-zone-right-hand = right hand
body-zone-left-thigh = left thigh
body-zone-right-thigh = right thigh
body-zone-left-shin = left shin
body-zone-right-shin = right shin
body-zone-left-foot = left foot
body-zone-right-foot = right foot

body-zone-head-detached = severed head
body-zone-left-upper-arm-detached = severed left upper arm
body-zone-right-upper-arm-detached = severed right upper arm
body-zone-left-forearm-detached = severed left forearm
body-zone-right-forearm-detached = severed right forearm
body-zone-left-hand-detached = severed left hand
body-zone-right-hand-detached = severed right hand
body-zone-left-thigh-detached = severed left thigh
body-zone-right-thigh-detached = severed right thigh
body-zone-left-shin-detached = severed left shin
body-zone-right-shin-detached = severed right shin
body-zone-left-foot-detached = severed left foot
body-zone-right-foot-detached = severed right foot

body-zones-doll-health = { CAPITALIZE($part) }: { $health }/{ $max }
body-zones-doll-missing = { CAPITALIZE($part) }: missing

body-zones-torn-off = { CAPITALIZE(POSS-ADJ($target)) } { $part } is torn off!
body-zones-shredded = { CAPITALIZE(POSS-ADJ($target)) } { $part } is ripped to shreds, the bone knocked out!
body-zones-tissue-torn = { CAPITALIZE(POSS-ADJ($target)) } { $organ } are torn!
body-zones-organ-failed = Something burst inside: your { $organ } stopped working.
body-zones-burst = { CAPITALIZE(POSS-ADJ($target)) } { $part } bursts apart!
body-zones-torn-open = { CAPITALIZE(POSS-ADJ($target)) } { $part } is torn open, organs spilling out!
body-zones-eye-knocked-out = { CAPITALIZE(POSS-ADJ($target)) } { $part } is knocked out!
body-zones-jaw-destroyed = { CAPITALIZE(POSS-ADJ($target)) } jaw is shattered and knocked out!
body-zones-body-torn-apart = { CAPITALIZE($target) } is torn apart: the head, arms and legs come off!
body-zones-severed = A blow cuts off { POSS-ADJ($target) } { $part }!
body-zones-remains-fall-apart = Nothing is left of { $target }: the remains fall apart!

part-dismantle-start = { CAPITALIZE($user) } starts cutting up the { $part }.
part-dismantle-done = The { $part } is cut to pieces.

body-zones-examine-missing = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } no { $part }.[/color]
body-zones-examine-torn-open = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } { $part } is torn open.[/color]
body-zones-examine-mangled = [color=orange]{ CAPITALIZE(POSS-ADJ($target)) } { $part } is mangled.[/color]
body-zones-examine-fracture = [color=orange]{ CAPITALIZE(POSS-ADJ($target)) } { $part } has a closed fracture.[/color]
body-zones-examine-open-fracture = [color=red]A broken bone sticks out of the wound in { POSS-ADJ($target) } { $part }.[/color]
body-zones-examine-crack = [color=yellow]The bone of { POSS-ADJ($target) } { $part } is cracked.[/color]
body-zones-examine-skinless = [color=red]The skin of { POSS-ADJ($target) } { $part } is gone.[/color]

body-zones-skin-torn = A piece of skin tears off { POSS-ADJ($target) } { $part }!

skinning-no-skin = There is no skin left here: { $part }.
skinning-start = { CAPITALIZE($user) } starts cutting the skin off { POSS-ADJ($target) } { $part }.
skinning-done = The skin of { POSS-ADJ($target) } { $part } is cut off.

circulation-numb = You can't feel your { $part }. No blood is getting there.
circulation-necrosis = { CAPITALIZE(POSS-ADJ($target)) } { $part } turns black and dies!
circulation-fountain = Blood gushes out of the stump of { POSS-ADJ($target) } { $part }!

circulation-examine-tourniquet = [color=yellow]A tourniquet is tied on the { $part }.[/color]
circulation-examine-tourniquet-stump = [color=yellow]A tourniquet is tied on the stump of the { $part }.[/color]
circulation-examine-fountain = [color=red]Blood gushes out of the stump of the { $part }![/color]
circulation-examine-numb = [color=orange]The { $part } is pale and cold.[/color]
circulation-examine-necrotic = [color=red]The { $part } is black and dead.[/color]

tourniquet-not-limb = A tourniquet only goes on an arm or a leg.
tourniquet-already = There is already a tourniquet on the { $part }.
tourniquet-start = { CAPITALIZE($user) } ties a tourniquet on the { $part }.
tourniquet-done = The tourniquet on the { $part } is tight.
tourniquet-verb-remove = Take off tourniquet: { $part }

body-zones-fracture = A bone snaps! { CAPITALIZE(POSS-ADJ($target)) } { $part } is broken.
body-zones-crack = The bone of your { $part } cracks.
body-zones-open-fracture = A broken bone sticks out of { POSS-ADJ($target) } { $part }!
body-zones-doll-fracture = { $text } (broken)

splint-no-fracture = The { $part } has no fracture or crack.
splint-open-fracture = An open fracture can't be set with a splint: { $part }.
splint-start = { CAPITALIZE($user) } starts splinting the { $part }.
splint-done = The { $part } has been splinted.

# For humans the upstream arm and leg are the forearm and the shin, the ears are the inner ear.
ent-OrganHumanArmLeft = left forearm
ent-OrganHumanArmRight = right forearm
ent-OrganHumanLegLeft = left shin
ent-OrganHumanLegRight = right shin
ent-OrganHumanEars = inner ear
