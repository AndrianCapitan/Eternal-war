markings-search = Поиск
-markings-selection = { $selectable ->
    [0] Больше отметин выбрать нельзя.
    [one] Можно выбрать ещё одну отметину.
   *[other] Можно выбрать ещё { $selectable }.
}
markings-limits = { $required ->
    [true] { $count ->
        [-1] Выберите хотя бы одну отметину.
        [0] Выбрать отметины нельзя, но почему-то нужно? Это ошибка.
        [one] Выберите одну отметину.
       *[other] Выберите от одной до {$count} отметин. { -markings-selection(selectable: $selectable) }
    }
   *[false] { $count ->
        [-1] Выберите любое количество отметин.
        [0] Выбрать отметины нельзя.
        [one] Можно выбрать до одной отметины.
       *[other] Можно выбрать до {$count} отметин. { -markings-selection(selectable: $selectable) }
    }
}
markings-reorder = Изменить порядок отметин

humanoid-marking-modifier-respect-limits = Соблюдать ограничения
humanoid-marking-modifier-respect-group-sex = Соблюдать ограничения группы и пола
humanoid-marking-modifier-base-layers = Базовые слои
humanoid-marking-modifier-enable = Включить
humanoid-marking-modifier-prototype-id = ID прототипа:

# Categories

markings-organ-Torso = Торс
markings-organ-Head = Голова
markings-organ-ArmLeft = Левая рука
markings-organ-ArmRight = Правая рука
markings-organ-HandRight = Правая кисть
markings-organ-HandLeft = Левая кисть
markings-organ-LegLeft = Левая нога
markings-organ-LegRight = Правая нога
markings-organ-FootLeft = Левая ступня
markings-organ-FootRight = Правая ступня
markings-organ-Eyes = Глаза

markings-layer-Special = Особое
markings-layer-Tail = Хвост
markings-layer-Tail-Moth = Крылья
markings-layer-Hair = Причёска
markings-layer-FacialHair = Растительность на лице
markings-layer-UndergarmentTop = Нижняя рубашка
markings-layer-UndergarmentBottom = Нижнее бельё
markings-layer-Chest = Грудь
markings-layer-Head = Голова
markings-layer-Snout = Морда
markings-layer-SnoutCover = Морда (поверх)
markings-layer-HeadSide = Голова (сбоку)
markings-layer-HeadTop = Голова (сверху)
markings-layer-Eyes = Глаза
markings-layer-OverEyes = Поверх глаз
markings-layer-RArm = Правая рука
markings-layer-LArm = Левая рука
markings-layer-RHand = Правая кисть
markings-layer-LHand = Левая кисть
markings-layer-RLeg = Правая нога
markings-layer-LLeg = Левая нога
markings-layer-RFoot = Правая ступня
markings-layer-LFoot = Левая ступня
markings-layer-Overlay = Наложение
markings-layer-TailOverlay = Наложение
