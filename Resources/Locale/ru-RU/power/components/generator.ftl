generator-clogged = {CAPITALIZE($generator)} резко глохнет!

portable-generator-verb-start = Запустить генератор
portable-generator-verb-start-msg-unreliable = Запустить генератор. Может получиться не с первого раза.
portable-generator-verb-start-msg-reliable = Запустить генератор.
portable-generator-verb-start-msg-unanchored = Сначала генератор нужно закрепить!
portable-generator-verb-stop = Остановить генератор
portable-generator-start-fail = Вы дёргаете шнур, но генератор не заводится.
portable-generator-start-success = Вы дёргаете шнур, и генератор с жужжанием оживает.

portable-generator-ui-title = Переносной генератор
portable-generator-ui-status-stopped = Остановлен:
portable-generator-ui-status-starting = Запускается:
portable-generator-ui-status-running = Работает:
portable-generator-ui-start = Запуск
portable-generator-ui-stop = Стоп
portable-generator-ui-target-power-label = Целевая мощность (кВт):
portable-generator-ui-efficiency-label = КПД:
portable-generator-ui-fuel-use-label = Расход топлива:
portable-generator-ui-fuel-left-label = Осталось топлива:
portable-generator-ui-clogged = В топливном баке обнаружены примеси!
portable-generator-ui-eject = Извлечь
portable-generator-ui-eta = (~{ $minutes } мин)
portable-generator-ui-unanchored = Не закреплён
portable-generator-ui-current-output = Текущий выход: {$voltage}
portable-generator-ui-network-stats = Сеть:
portable-generator-ui-network-stats-value = { POWERWATTS($supply) } / { POWERWATTS($load) }
portable-generator-ui-network-stats-not-connected = Не подключён

power-switchable-generator-examine = Выход питания переключён на {$voltage}.
power-switchable-generator-switched = Выход переключён на {$voltage}!

power-switchable-voltage = { $voltage ->
    [HV] [color=orange]ВН[/color]
    [MV] [color=yellow]СН[/color]
    *[LV] [color=green]НН[/color]
}
power-switchable-switch-voltage = Переключить на {$voltage}

fuel-generator-verb-disable-on = Сначала выключите генератор!
