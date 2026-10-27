## Скелет: кости, вырванные из тела, и их разборка (средняя кнопка мыши)

ent-BoneAssemblySkeleton = скелет
    .desc = Череп, позвоночник, рёбра и таз, всё ещё держатся вместе.
ent-BoneAssemblyTrunk = таз, позвоночник и рёбра
    .desc = Кости туловища, всё ещё держатся вместе.
ent-BoneAssemblyPelvisSpine = таз и позвоночник
    .desc = Позвоночник, всё ещё сидящий в тазу.
ent-BoneAssemblyRibsSpine = рёбра с позвоночником
    .desc = Грудная клетка, всё ещё висящая на позвоночнике.
ent-BoneAssemblySpine = позвоночник
    .desc = Целый позвоночник. Руками не разломать.
ent-BoneAssemblySkullJaw = череп с челюстью
    .desc = Череп вместе с нижней челюстью.

skeleton-extract-nothing = Оголённых костей нет: сначала снимите кожу и мышцы.
skeleton-extract-start = { CAPITALIZE($user) } вырывает кости из тела: { $target }!
skeleton-extract-done = { CAPITALIZE($user) } вырывает кости из тела: { $bones }.
skeleton-take-out = { CAPITALIZE($user) } разбирает { $from }: отделено — { $bones }.
skeleton-too-strong = { CAPITALIZE($bones) } руками не разобрать: нужна пила или дробящее оружие.
skeleton-saw-start = { CAPITALIZE($user) } распиливает { $bones }.
skeleton-saw-done = { CAPITALIZE($bones) } распилен надвое.
skeleton-cracked = { CAPITALIZE($bones) } трескается.
skeleton-broken-in-two = { CAPITALIZE($bones) } разламывается надвое!
skeleton-examine-broken = [color=red]Кости сломаны.[/color]
skeleton-examine-take-apart = Можно разобрать средней кнопкой мыши.
skeleton-examine-saw = Можно распилить пилой (средней кнопкой мыши с пилой в руке) или разбить дробящим оружием.

ui-options-function-eternal-war-take-apart = Разобрать (кости)
