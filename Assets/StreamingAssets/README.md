前端组的**唯一任务**是把 `final_example.json` 文件忠实地通过 Unity 3D 的 UI 展现出来。这要求两个方面的工作：

1. **数据解析**：前端需要编写代码来读取 `final_example.json` 文件，并将其中的数据结构解析成 Unity 3D 可以使用的格式。
2. **UI 展现**：前端需要设计和实现一个用户界面，能够根据解析后的数据动态地展示内容。这可能包括文本、图片、按钮等 UI 元素。

所有代码都位于 `Assets/Scripts` 文件夹下，包括以下几个文件：

1. `MapData.cs`：负责解析 `final_example.json` 中的 `mapdata` 部分，创建一个 `mapWidth` x `mapWidth` 的地图，每个格子的高度由 `rows` 二维数组中的值决定。
2. `PlayerData.cs`：负责解析 `final_example.json` 中的 `playerData` 部分，提取双方玩家的 ID；在每个 `gameRounds` 结束后，提取 `gameRounds/score`；根据 `gameRounds/end` 的值来判断游戏是否结束。将这些信息展示在 UI 上，方便玩家查看当前游戏状态和分数情况。
3. `SoldiersData.cs`：负责解析 `soldiersData` 部分，提取每个士兵的 ID、职业、归属玩家、初始位置、初始状态等信息，根据其职业和归属玩家决定士兵的颜色和形象；在每个回合结束时，解析 `gameRounds/stats` 部分，提取当前每个士兵的最终状态信息，根据 `soldierID` 来更新 UI 上对应士兵的状态显示（如血量、位置等）。
4. `Actions.cs`：负责解析 `gameRounds/actions` 部分，提取每个回合的所有动作信息，根据 `actionType` 来决定动作的类型（移动、普攻、技能）：移动需要根据 `soldierID` 和 `path` 展示指定士兵的行动路径，普攻需要根据 `soldierID` 、`damageDealt` 展示攻击动画和伤害数值，技能需要根据 `ability` 、`soldierID` 、`targetPosition` 、`damageDealt` 展示技能名、技能动画和伤害数值。
5. `GameRounds.cs`：负责整体管理游戏回合的流程，协调 `Actions.cs`、`Stats.cs`、`PlayerData.cs` 等脚本的调用，确保每个回合的动作和状态更新能够正确地在 UI 上展示出来。

考虑到美工资源尚未准备好，前端可以先使用简单的占位符（如方块、圆形等）来代表不同类型的士兵和 UI 元素，确保功能实现后再替换为正式的美术资源。