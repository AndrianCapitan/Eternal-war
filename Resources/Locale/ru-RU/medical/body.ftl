## Кровь
bloodstream-component-looks-pale = [color=bisque]Выглядит бледно.[/color]
bloodstream-component-slight-bleeding = [color=#ffa8a8]Сочится кровь.[/color]
bloodstream-component-bleeding = [color=#e65353]Идёт кровь.[/color]
bloodstream-component-strong-bleeding = [color=#d10a0a]Сильно кровоточит.[/color]
bloodstream-component-massive-bleeding = [color=#d4003c]Кровь хлещет из тела![/color]

bloodstream-component-wounds-cauterized = Вы чувствуете, как ваши раны болезненно закрываются!

## Сканер тела
body-scanner-display-title = Сканер тела
body-scanner-display-health-label = Здоровье:
body-scanner-display-body-part-damage-text = Урон: {$damage}

## Клонирование
accept-cloning-window-title = Клонирующая машина
accept-cloning-window-prompt-text-part = Вас клонируют!
                                         Перенести вашу душу в тело клона?
accept-cloning-window-accept-button = Да
accept-cloning-window-deny-button = Нет

## Болезни и разложение
disease-vomit = {CAPITALIZE($person)} тошнит.

ammonia-smell = Чем-то резко пахнет!

perishable-1 = [color=green]Труп ещё выглядит свежим.[/color]
perishable-2 = [color=orangered]Труп выглядит относительно свежим.[/color]
perishable-3 = [color=red]Труп выглядит не очень свежим.[/color]

perishable-1-nonmob = [color=green]Выглядит ещё свежим.[/color]
perishable-2-nonmob = [color=orangered]Выглядит относительно свежим.[/color]
perishable-3-nonmob = [color=red]Выглядит не очень свежим.[/color]

rotting-rotting = [color=orange]Труп гниёт![/color]
rotting-bloated = [color=orangered]Труп вздулся![/color]
rotting-extremely-bloated = [color=red]Труп чудовищно вздулся![/color]

rotting-rotting-nonmob = [color=orange]Гниёт![/color]
rotting-bloated-nonmob = [color=orangered]Вздулось![/color]
rotting-extremely-bloated-nonmob = [color=red]Чудовищно вздулось![/color]

## Метаболизм
metabolism-component-is-comfortable = Вам комфортно
metabolism-component-is-sweating = Вы потеете
metabolism-component-is-shivering = Вас знобит

metabolism-stage-respiration = Дыхание
metabolism-stage-digestion = Пищеварение
metabolism-stage-bloodstream = Кровоток
metabolism-stage-metabolites = Метаболиты

metabolism-stage-plant = Метаболизм растений

metabolizer-type-animal = Животное
metabolizer-type-bloodsucker = Кровосос
metabolizer-type-dragon = Дракон
metabolizer-type-human = Человек
metabolizer-type-slime = Слайм
metabolizer-type-vox = Вокс
metabolizer-type-rat = Крыса
metabolizer-type-plant = Растение
metabolizer-type-dwarf = Дворф
metabolizer-type-moth = Ниан
metabolizer-type-arachnid = Арахнид
metabolizer-type-changeling = Генокрад

## Морг и крематорий
crematorium-entity-storage-component-on-examine-details-is-burning = {CAPITALIZE($owner)} [color=red]работает[/color]!
crematorium-entity-storage-component-on-examine-details-has-contents = Индикатор содержимого [color=green]горит[/color] — внутри что-то есть.
crematorium-entity-storage-component-on-examine-details-empty = Индикатор содержимого не горит — внутри пусто.
crematorium-entity-storage-component-is-cooking-safety-message = Безопасность прежде всего — не во время работы!
crematorium-entity-storage-component-suicide-message = Вы кремируете себя!
crematorium-entity-storage-component-suicide-message-others = {CAPITALIZE($victim)} кремирует себя!

cremate-verb-get-data-text = Кремировать

morgue-entity-storage-component-on-examine-details-body-has-soul = Индикатор содержимого [color=green]зелёный[/color] — это тело ещё можно спасти!
morgue-entity-storage-component-on-examine-details-body-has-no-soul = Индикатор содержимого [color=red]красный[/color] — внутри мёртвое тело! Ой, погодите...
morgue-entity-storage-component-on-examine-details-has-contents = Индикатор содержимого [color=yellow]жёлтый[/color] — внутри что-то есть.
morgue-entity-storage-component-on-examine-details-empty = Индикатор содержимого не горит — внутри пусто.

## Импланты
implanter-component-implanting-target = {CAPITALIZE($user)} пытается что-то вам имплантировать!
implanter-component-draw-target = {CAPITALIZE($user)} пытается что-то из вас извлечь!
implanter-component-implant-failed = {CAPITALIZE($implant)}: нельзя установить ({$target})!
implanter-draw-failed-permanent = {CAPITALIZE($implant)} сросся с носителем ({$target}), его нельзя извлечь!
implanter-draw-failed = Вы попытались извлечь имплант, но ничего не нашли.
implanter-draw-failed-catastrophically = Имплантер ничего не находит и катастрофически ломается, выбрасывая генетический материал в руку ({$user})!
implanter-component-implant-already = {CAPITALIZE($target)}: {$implant} уже установлен!

implanter-set-draw-verb = Выбрать извлекаемый имплант
implanter-set-draw-window = Выбор извлекаемого импланта
implanter-set-draw-info = Выберите тип импланта, который извлечёт этот имплантер:
implanter-set-draw-type = Тип импланта:

implanter-draw-text = Извлечь
implanter-inject-text = Ввести

implanter-empty-text = Пусто

implanter-label-inject = [color=green]{$implantName}[/color]
    Режим: [color=white]{$modeString}[/color]

implanter-label-draw = [color=red]{$implantName}[/color]
    Режим: [color=white]{$modeString}[/color]

implanter-contained-implant-text = [color=green]{$desc}[/color]
