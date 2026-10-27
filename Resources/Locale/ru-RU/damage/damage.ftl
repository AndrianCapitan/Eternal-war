## Damage examines
damage-examinable-verb-text = Урон
damage-examinable-verb-message = Посмотреть значения урона.

damage-hitscan = луч
damage-projectile = снаряд
damage-melee = ближний бой
damage-throw = бросок

damage-examine = Наносит следующий урон:
damage-examine-type = Наносит следующий урон ([color=cyan]{$type}[/color]):
damage-value = - [color=red]{$amount}[/color] ед. урона типа [color=yellow]{$type}[/color].
damage-none = Не наносит урона.

## Крики от урона
damage-force-say-message-wrap = {$message}-{$suffix}
damage-force-say-message-wrap-no-suffix = {$message}-

damage-force-say-1 = КХА!
damage-force-say-2 = ГЛХ!
damage-force-say-3 = УФ!
damage-force-say-4 = АРГХ!
damage-force-say-5 = АЙ!
damage-force-say-6 = УРГХ!
damage-force-say-7 = ХРНК!

damage-force-say-sleep-1 = хррр...

damage-force-say-numb-1 = ох-
damage-force-say-numb-2 = ай-
damage-force-say-numb-3 = уф-
damage-force-say-numb-4 = ах-
damage-force-say-numb-5 = угх-

## Группы урона
damage-group-brute = Механический
damage-group-burn = Ожоги
damage-group-airloss = Удушье
damage-group-toxin = Токсины
damage-group-genetic = Генетический
damage-group-metaphysical = Метафизический

## Всплывающие числа урона
-damage-popup-component-type =
    { $setting ->
        [combined] Совмещённый
        [total] Общий
        [delta] Изменение
        [hit] Удар
       *[other] Неизвестно
    }

damage-popup-component-switched = Режим цели: { -damage-popup-component-type(setting: $setting) }

## Типы урона
damage-type-asphyxiation = Асфиксия
damage-type-bloodloss = Кровопотеря
damage-type-blunt = Дробящий
damage-type-cellular = Клеточный
damage-type-caustic = Кислотный
damage-type-cold = Холод
damage-type-heat = Термический
damage-type-piercing = Колотый
damage-type-poison = Яд
damage-type-radiation = Радиация
damage-type-shock = Электрический
damage-type-slash = Режущий
damage-type-structural = Структурный
damage-type-holy = Святой

## Прочее
mouth-taste-metal = Вы чувствуете во рту металлический привкус!

rejuvenate-verb-get-data-text = Омолодить

melee-stamina = Не хватает выносливости
slow-on-damage-modifier-examine = Замедление от ран снижено на [color=yellow]{$mod}%[/color]
stamina-resistance-coefficient-value = - Урон [color=lightyellow]выносливости[/color] снижен на [color=lightblue]{$value}%[/color].
