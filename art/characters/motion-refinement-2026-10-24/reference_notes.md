# 动作参考与观察边界

方向：现代战术射击游戏的负重姿态、稳定持枪、肘膝支撑和受力节奏。用户参考《战地6》《三角洲行动》，不使用其模型、动画或贴图。

- EA 官方 Kinesthetic Combat System 介绍：https://news.ea.com/press-releases/press-releases-details/2025/Battlefield-6s-Game-Changing-Multiplayer-and-More-Revealed-During-Blockbuster-Global-Event/default.aspx 。只作为移动/枪械协同的设计方向，未获得可逐帧分析的官方第三人称匍匐素材，不声称复刻其动画。
- 三角洲官方更新说明：https://deltaforce.garena.com/en/news/announcement/YVRATP 。包含 prone movement 手部动画改进，只提供问题域参考。
- 检索实拍趴伏支撑图：https://www.defense.gov/Multimedia/Photos/igphoto/2001874529/ 、https://www.army.mil/article/62495/marne_soldiers_always_in_ready_position_when_it_comes_to_training 。原图工具访问失败，未声称实际查看；未复制到交付中。

实际诊断依据：0.57 的 Unity 趴伏侧视与本次真实蒙皮连续帧。核心修正是几何/接触约束：骨盆转至水平、分段腰胸、肘与膝的接触高度；单侧收腿与对侧伸腿、撑地/恢复分段；按实际位移推进步相。动作节奏属于本项目原创设计，不是动作捕捉。
