body-zone-head = голова
body-zone-left-eye = левый глаз
body-zone-right-eye = правый глаз
body-zone-mouth = рот
body-zone-chest = грудь
body-zone-abdomen = живот
body-zone-genitals = половая зона
body-zone-left-upper-arm = левое плечо
body-zone-right-upper-arm = правое плечо
body-zone-left-forearm = левое предплечье
body-zone-right-forearm = правое предплечье
body-zone-left-hand = левая кисть
body-zone-right-hand = правая кисть
body-zone-left-thigh = левое бедро
body-zone-right-thigh = правое бедро
body-zone-left-shin = левая голень
body-zone-right-shin = правая голень
body-zone-left-foot = левая стопа
body-zone-right-foot = правая стопа

# Отвалившаяся целая часть тела вместе со всем, что на ней висит: предплечье с кистью, голень со стопой.
body-zone-head-detached = оторванная голова
body-zone-left-upper-arm-detached = оторванная левая рука
body-zone-right-upper-arm-detached = оторванная правая рука
body-zone-left-forearm-detached = оторванное левое предплечье
body-zone-right-forearm-detached = оторванное правое предплечье
body-zone-left-hand-detached = оторванная левая кисть
body-zone-right-hand-detached = оторванная правая кисть
body-zone-left-thigh-detached = оторванная левая нога
body-zone-right-thigh-detached = оторванная правая нога
body-zone-left-shin-detached = оторванная левая голень
body-zone-right-shin-detached = оторванная правая голень
body-zone-left-foot-detached = оторванная левая стопа
body-zone-right-foot-detached = оторванная правая стопа

body-zones-doll-health = { CAPITALIZE($part) }: { $health }/{ $max }
body-zones-doll-missing = { CAPITALIZE($part) }: отсутствует

body-zones-torn-off = { $target }: { $part } отрывается!
body-zones-shredded = { $target }: { $part } — разорвано в клочья, кость выбита!
body-zones-tissue-torn = { $target }: разорваны { $organ }!
body-zones-organ-failed = Что-то внутри лопнуло: { $organ } больше не работает.
body-zones-burst = { $target }: { $part } разрывается на куски!
body-zones-torn-open = { $target }: страшная рваная рана ({ $part }), органы вываливаются наружу!
body-zones-eye-knocked-out = { $target }: { $part } выбит!
body-zones-jaw-destroyed = { $target }: челюсть раздроблена и выбита!
body-zones-body-torn-apart = { $target }: тело разорвано — голова, руки и ноги отрываются!
body-zones-severed = { $target }: удар отсекает конечность — { $part }!
body-zones-remains-fall-apart = От тела ничего не осталось: { $target } разваливается на куски!

part-dismantle-start = { CAPITALIZE($user) } разделывает: { $part }.
part-dismantle-done = Разделано на части: { $part }.

body-zones-examine-missing = [color=red]Отсутствует: { $part }.[/color]
body-zones-examine-torn-open = [color=red]Страшная рваная рана: { $part }.[/color]
body-zones-examine-mangled = [color=orange]Изувечено: { $part }.[/color]
body-zones-examine-fracture = [color=orange]Закрытый перелом: { $part }.[/color]
body-zones-examine-open-fracture = [color=red]Открытый перелом, кость торчит из раны: { $part }.[/color]
body-zones-examine-crack = [color=yellow]Трещина в кости: { $part }.[/color]
body-zones-examine-skinless = [color=red]Содрана кожа: { $part }.[/color]

body-zones-skin-torn = { $target }: кусок кожи срывается ({ $part })!

skinning-no-skin = Здесь уже нет кожи: { $part }.
skinning-start = { CAPITALIZE($user) } срезает кожу: { $target }, { $part }.
skinning-done = { $target }: кожа срезана ({ $part }).

body-zones-fracture = { $target }: хруст кости! Перелом — { $part }.
body-zones-crack = Кость треснула: { $part }.
body-zones-open-fracture = { $target }: обломок кости торчит из раны ({ $part })!
body-zones-doll-fracture = { $text } (перелом)

splint-no-fracture = Здесь нет ни перелома, ни трещины: { $part }.
splint-open-fracture = Открытый перелом шиной не вправить: { $part }.
splint-start = { CAPITALIZE($user) } накладывает шину: { $part }.
splint-done = Шина наложена, кость вправлена: { $part }.

ent-Splint = шина
    .desc = Две планки и бинт. Прицельтесь в сломанную часть тела и наложите шину, чтобы вправить кость.

## Кровообращение

circulation-numb = Вы не чувствуете: { $part }. Туда не поступает кровь.
circulation-necrosis = { $target }: { $part } — ткань почернела и отмерла!
circulation-fountain = { $target }: из культи хлещет кровь фонтаном ({ $part })!

circulation-examine-tourniquet = [color=yellow]Наложен жгут: { $part }.[/color]
circulation-examine-tourniquet-stump = [color=yellow]Жгут на культе: { $part }.[/color]
circulation-examine-fountain = [color=red]Из культи хлещет кровь: { $part }![/color]
circulation-examine-numb = [color=orange]Бледное и холодное: { $part }.[/color]
circulation-examine-necrotic = [color=red]Омертвение, ткань почернела: { $part }.[/color]

tourniquet-not-limb = Жгут накладывают только на руку или ногу.
tourniquet-already = Здесь уже есть жгут: { $part }.
tourniquet-start = { CAPITALIZE($user) } накладывает жгут: { $part }.
tourniquet-done = Жгут затянут: { $part }.
tourniquet-verb-remove = Снять жгут: { $part }

## Кости

ent-BoneSkull = череп
    .desc = Когда-то в нём кто-то думал.
ent-BoneJaw = нижняя челюсть
    .desc = Без неё не поговоришь и не поешь.
ent-BoneSpineThoracic = грудной отдел позвоночника
    .desc = Двенадцать позвонков, к которым крепятся рёбра.
ent-BoneRibs = рёбра
    .desc = Грудная клетка. Без лёгких и сердца выглядит пустовато.
ent-BoneSpineLumbar = поясничный отдел позвоночника
    .desc = Пять крупных позвонков. Держат на себе весь верх тела.
ent-BonePelvis = тазовая кость
    .desc = На ней держалось всё остальное.
ent-BoneHumerusLeft = левая плечевая кость
    .desc = Самая длинная кость руки.
ent-BoneHumerusRight = правая плечевая кость
    .desc = Самая длинная кость руки.
ent-BoneForearmLeft = кости левого предплечья
    .desc = Локтевая и лучевая кости.
ent-BoneForearmRight = кости правого предплечья
    .desc = Локтевая и лучевая кости.
ent-BoneHandLeft = кости левой кисти
    .desc = Множество мелких косточек.
ent-BoneHandRight = кости правой кисти
    .desc = Множество мелких косточек.
ent-BoneFemurLeft = левая бедренная кость
    .desc = Самая длинная и прочная кость тела.
ent-BoneFemurRight = правая бедренная кость
    .desc = Самая длинная и прочная кость тела.
ent-BoneShinLeft = кости левой голени
    .desc = Большеберцовая и малоберцовая кости.
ent-BoneShinRight = кости правой голени
    .desc = Большеберцовая и малоберцовая кости.
ent-BoneFootLeft = кости левой стопы
    .desc = Пяточная кость и фаланги пальцев.
ent-BoneFootRight = кости правой стопы
    .desc = Пяточная кость и фаланги пальцев.

## Мышцы

ent-MusclesFace = лицевые мышцы
    .desc = Ими улыбаются, хмурятся и жуют.
ent-MusclesChest = мышцы груди
    .desc = Грудные и межрёберные мышцы.
ent-MusclesAbdomen = мышцы живота и таза
    .desc = Пресс и мышцы тазового дна.
ent-MusclesUpperArmLeft = мышцы левого плеча
    .desc = Бицепс и трицепс.
ent-MusclesUpperArmRight = мышцы правого плеча
    .desc = Бицепс и трицепс.
ent-MusclesForearmLeft = мышцы левого предплечья
    .desc = Сгибают и разгибают кисть и пальцы.
ent-MusclesForearmRight = мышцы правого предплечья
    .desc = Сгибают и разгибают кисть и пальцы.
ent-MusclesHandLeft = мышцы левой кисти
    .desc = Мелкие мышцы ладони и пальцев.
ent-MusclesHandRight = мышцы правой кисти
    .desc = Мелкие мышцы ладони и пальцев.
ent-MusclesThighLeft = мышцы левого бедра
    .desc = Самые сильные мышцы тела.
ent-MusclesThighRight = мышцы правого бедра
    .desc = Самые сильные мышцы тела.
ent-MusclesShinLeft = мышцы левой голени
    .desc = Икроножная мышца и её соседи.
ent-MusclesShinRight = мышцы правой голени
    .desc = Икроножная мышца и её соседи.
ent-MusclesFootLeft = мышцы левой стопы
    .desc = Держат свод стопы.
ent-MusclesFootRight = мышцы правой стопы
    .desc = Держат свод стопы.

## Кровеносные сосуды

ent-VesselsHead = кровеносные сосуды головы
    .desc = Сонные артерии и яремные вены.
ent-VesselsChest = кровеносные сосуды груди
    .desc = Аорта, полые вены и лёгочные сосуды.
ent-VesselsAbdomen = кровеносные сосуды живота
    .desc = Брюшная аорта и её ветви.
ent-VesselsUpperArmLeft = кровеносные сосуды левого плеча
    .desc = Плечевая артерия и вены.
ent-VesselsUpperArmRight = кровеносные сосуды правого плеча
    .desc = Плечевая артерия и вены.
ent-VesselsForearmLeft = кровеносные сосуды левого предплечья
    .desc = Лучевая и локтевая артерии.
ent-VesselsForearmRight = кровеносные сосуды правого предплечья
    .desc = Лучевая и локтевая артерии.
ent-VesselsThighLeft = кровеносные сосуды левого бедра
    .desc = Бедренная артерия. Её ранение смертельно опасно.
ent-VesselsThighRight = кровеносные сосуды правого бедра
    .desc = Бедренная артерия. Её ранение смертельно опасно.
ent-VesselsShinLeft = кровеносные сосуды левой голени
    .desc = Большеберцовые артерии.
ent-VesselsShinRight = кровеносные сосуды правой голени
ent-VesselsFootLeft = кровеносные сосуды левой стопы
ent-VesselsFootRight = кровеносные сосуды правой стопы
    .desc = Большеберцовые артерии.

## Органы

ent-OrganHumanUpperArmLeft = левое плечо
ent-OrganHumanUpperArmRight = правое плечо
ent-OrganHumanThighLeft = левое бедро
ent-OrganHumanThighRight = правое бедро
ent-OrganHumanEyeLeft = левый глаз
ent-OrganHumanEyeRight = правый глаз
ent-OrganHumanEarLeft = левое внутреннее ухо
ent-OrganHumanEarRight = правое внутреннее ухо
ent-OrganHumanLungLeft = левое лёгкое
ent-OrganHumanLungRight = правое лёгкое
ent-OrganHumanKidneyLeft = левая почка
ent-OrganHumanKidneyRight = правая почка
ent-OrganHumanSpleen = селезёнка
ent-OrganHumanIntestines = кишечник
ent-OrganTestes = семенники
    .desc = Мужские половые железы.
ent-OrganOvaries = яичники
    .desc = Женские половые железы.
ent-OrganPenis = пенис
    .desc = Мужской половой орган.
ent-OrganVagina = влагалище
    .desc = Женский половой орган.

## Кожа

ent-SkinFace = кожа лица
ent-SkinChest = кожа груди
ent-SkinAbdomen = кожа живота
ent-SkinUpperArmLeft = кожа левого плеча
ent-SkinUpperArmRight = кожа правого плеча
ent-SkinForearmLeft = кожа левого предплечья
ent-SkinForearmRight = кожа правого предплечья
ent-SkinHandLeft = кожа левой кисти
ent-SkinHandRight = кожа правой кисти
ent-SkinThighLeft = кожа левого бедра
ent-SkinThighRight = кожа правого бедра
ent-SkinShinLeft = кожа левой голени
ent-SkinShinRight = кожа правой голени
ent-SkinFootLeft = кожа левой стопы
ent-SkinFootRight = кожа правой стопы
