## ХимМастер
chem-master-component-activate-no-hands = У вас нет рук.
chem-master-component-cannot-put-entity-message = Это нельзя положить в ХимМастер!

chem-master-bound-user-interface-title = ХимМастер 4000

chem-master-window-input-tab = Ввод
chem-master-window-output-tab = Вывод
chem-master-window-container-label = Ёмкость
chem-master-window-eject-button = Извлечь
chem-master-window-no-container-loaded-text = Ёмкость не загружена.
chem-master-window-buffer-text = Буфер
chem-master-window-buffer-label = буфер:
chem-master-window-buffer-all-amount = Всё
chem-master-window-buffer-empty-text = Буфер пуст.
chem-master-window-buffer-low-text = В буфере недостаточно раствора
chem-master-window-transfer-button = Перенести
chem-master-window-discard-button = Удалить
chem-master-window-packaging-text = Фасовка
chem-master-current-text-label = Этикетка:
chem-master-window-pills-label = Таблетки:
chem-master-window-pill-type-label = Вид таблетки:
chem-master-window-pills-number-label = Количество:
chem-master-window-dose-label = Доза (ед.):
chem-master-window-create-button = Создать
chem-master-window-bottles-label = Бутылочки:
chem-master-window-sort-type-none = Сортировка: сначала старые
chem-master-window-sort-type-alphabetical = Сортировка: по алфавиту
chem-master-window-sort-type-quantity = Сортировка: по количеству
chem-master-window-sort-type-latest = Сортировка: сначала новые
chem-master-output-buffer-draw = Буфер
chem-master-output-beaker-draw = Стакан
chem-master-window-no-beaker-text = Стакан не загружен
chem-master-window-beaker-empty-text = Стакан пуст
chem-master-window-beaker-low-text = В стакане недостаточно раствора
chem-master-output-source = Источник для фасовки:
chem-master-no-source = Нет источника

## Шприцы и инъекторы
injector-volume-transfer-label = Объём: [color=white]{$currentVolume}/{$totalVolume} ед.[/color]
    Режим: [color=white]{$modeString}[/color] ([color=white]{$transferVolume} ед.[/color])
injector-volume-label = Объём: [color=white]{$currentVolume}/{$totalVolume} ед.[/color]
    Режим: [color=white]{$modeString}[/color]
injector-toggle-verb-text = Переключить режим инъектора

injector-component-inject-mode-name = ввод
injector-component-draw-mode-name = забор
injector-component-dynamic-mode-name = автоматический
injector-component-mode-changed-text = Режим: {$mode}
injector-component-transfer-success-message = Вы переливаете {$amount} ед. в {$target}.
injector-component-transfer-success-message-self = Вы вводите себе {$amount} ед.
injector-component-inject-success-message = Вы вводите {$amount} ед. Цель: {$target}!
injector-component-inject-success-message-self = Вы вводите себе {$amount} ед.!
injector-component-draw-success-message = Вы набираете {$amount} ед. Источник: {$target}.
injector-component-draw-success-message-self = Вы набираете у себя {$amount} ед.

injector-component-target-already-full-message = {CAPITALIZE($target)}: уже полно!
injector-component-target-already-full-message-self = Вы уже полны!
injector-component-target-is-empty-message = {CAPITALIZE($target)}: пусто!
injector-component-target-is-empty-message-self = В вас пусто!
injector-component-cannot-toggle-draw-message = Слишком полон для забора!
injector-component-cannot-toggle-inject-message = Нечего вводить!
injector-component-cannot-toggle-dynamic-message = Нельзя включить автоматический режим!
injector-component-empty-message = {CAPITALIZE($injector)}: пусто!
injector-component-blocked-user = Защитное снаряжение помешало уколу!
injector-component-blocked-other = Броня цели ({$target}) помешала уколу ({$user})!
injector-component-cannot-transfer-message = Сюда нельзя перелить: {$target}!
injector-component-cannot-transfer-message-self = В себя нельзя перелить!
injector-component-cannot-inject-message = Сюда нельзя ввести: {$target}!
injector-component-cannot-inject-message-self = Себе нельзя ввести!
injector-component-cannot-draw-message = Отсюда нельзя набрать: {$target}!
injector-component-cannot-draw-message-self = У себя нельзя набрать!
injector-component-ignore-mobs = Этот инъектор работает только с ёмкостями!

injector-component-needle-injecting-user = Вы начинаете вводить иглу.
injector-component-needle-injecting-target = {CAPITALIZE($user)} пытается воткнуть в вас иглу!
injector-component-needle-drawing-user = Вы начинаете набирать иглой.
injector-component-needle-drawing-target = {CAPITALIZE($user)} пытается взять у вас пробу иглой!
injector-component-spray-injecting-user = Вы начинаете прикладывать распылитель.
injector-component-spray-injecting-target = {CAPITALIZE($user)} пытается приложить к вам распылитель!

injector-component-feel-prick-message = Вы чувствуете лёгкий укол!

## Смешивание
mixing-verb-default-mix = смешать
mixing-verb-default-grind = измельчить
mixing-verb-default-juice = выжать
mixing-verb-default-condense = сконденсировать
mixing-verb-centrifuge = центрифугирование
mixing-verb-electrolysis = электролиз
mixing-verb-holy = освятить
mixing-verb-stir = размешать
mixing-verb-shake = взболтать

default-mixing-success = Вы смешиваете {$mixed}: {$mixer}
bible-mixing-success = Вы освящаете {$mixed}: {$mixer}
spoon-mixing-success = Вы размешиваете {$mixed}: {$mixer}
handheld-centrifuge-success = Вы разделяете химикаты: {$mixed}

## Химический раздатчик
reagent-dispenser-component-activate-no-hands = У вас нет рук.
reagent-dispenser-component-cannot-put-entity-message = Это нельзя положить в раздатчик!

reagent-dispenser-bound-user-interface-title = Химический раздатчик

reagent-dispenser-window-amount-to-dispense-label = Количество
reagent-dispenser-window-clear-button = Очистить
reagent-dispenser-window-eject-button = Извлечь
reagent-dispenser-window-eject-container-button = ⏏
reagent-dispenser-window-no-container-loaded-text = Ёмкость не загружена.
reagent-dispenser-window-reagent-name-not-found-text = Название реагента не найдено
reagent-dispenser-window-unknown-reagent-text = Неизвестный реагент
reagent-dispenser-window-quantity-label-text = {$quantity} ед.

## Прочее
rehydratable-component-expands-message = {CAPITALIZE($owner)} разбухает!
scoopable-component-popup = Вы зачерпываете {$scooped}. Ёмкость: {$beaker}.

solution-container-mixer-activate = Включить
solution-container-mixer-no-power = Нет питания!
solution-container-mixer-popup-nothing-to-mix = Внутри пусто!

scannable-solution-verb-text = Раствор
scannable-solution-verb-message = Изучить химический состав.
scannable-solution-main-text = Содержит следующие химикаты:
scannable-solution-empty-container = Химикатов не содержит.
scannable-solution-chemical = - {$amount} ед. [color={$color}]{$type}[/color]
scannable-solution-temperature = Температура раствора: {$temperature} K

spike-solution-generic = Вы подмешиваете {$spike-entity}. Куда: {$spiked-entity}.
spike-solution-empty-generic = {CAPITALIZE($spike-entity)} не растворяется. Куда: {$spiked-entity}.
spike-solution-egg = Вы разбиваете {$spike-entity}. Куда: {$spiked-entity}.
spike-solution-mix = Вы вмешиваете {$spike-entity}. Куда: {$spiked-entity}.

solution-status-volume = Объём: [color=white]{$currentVolume}/{$maxVolume} ед.[/color]
solution-status-transfer = Перенос: [color=white]{$volume} ед.[/color]

comp-solution-transfer-fill-normal = Вы переливаете {$amount} ед. Откуда: {$owner}. Куда: {$target}.
comp-solution-transfer-fill-fully = Вы наполняете до краёв, {$amount} ед. Откуда: {$owner}. Куда: {$target}.
comp-solution-transfer-transfer-solution = Вы переливаете {$amount} ед. Куда: {$target}.

comp-solution-transfer-is-empty = {CAPITALIZE($target)}: пусто!
comp-solution-transfer-is-full = {CAPITALIZE($target)}: полно!

comp-solution-transfer-verb-custom-amount = Своё значение
comp-solution-transfer-verb-amount = {$amount} ед.
comp-solution-transfer-verb-toggle = Переключить на {$amount} ед.

comp-solution-transfer-set-amount = Объём переноса: {$amount} ед.
comp-solution-transfer-set-amount-max = Макс.: {$amount} ед.
comp-solution-transfer-set-amount-min = Мин.: {$amount} ед.

transformable-container-component-glass = стакан: {$reagent}

effect-sleepy = Вас клонит в сон.

## Осмотр растворов
shared-solution-container-component-on-examine-main-text = Содержит [color={$color}]{$colorName} {$desc}[/color] { $chemCount ->
    [1] вещество.
   *[other] вещество — смесь химикатов.
    }

examinable-solution-has-recognizable-chemicals = В растворе можно узнать: {$recognizedString}.
examinable-solution-recognized = [color={$color}]{$chemical}[/color]

examinable-solution-on-examine-volume = Раствор: { $fillLevel ->
    [exact] [color=white]{$current}/{$max} ед.[/color]
   *[other] [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
}

examinable-solution-on-examine-volume-no-max = Раствор: { $fillLevel ->
    [exact] [color=white]{$current} ед.[/color]
   *[other] [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
}

examinable-solution-on-examine-volume-puddle = Лужа { $fillLevel ->
    [exact] — [color=white]{$current} ед.[/color]
    [full] огромная и растекается!
    [mostlyfull] огромная и растекается!
    [halffull] глубокая и течёт.
    [halfempty] очень глубокая.
   *[mostlyempty] собирается в одну.
    [empty] распадается на несколько маленьких.
}

-solution-vague-fill-level =
    { $fillLevel ->
        [full] [color=white]полный[/color]
        [mostlyfull] [color=#DFDFDF]почти полный[/color]
        [halffull] [color=#C8C8C8]наполовину полный[/color]
        [halfempty] [color=#C8C8C8]наполовину пустой[/color]
        [mostlyempty] [color=#A4A4A4]почти пустой[/color]
       *[empty] [color=gray]пустой[/color]
    }
