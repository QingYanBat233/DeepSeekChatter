# DeepSeekChatter
这是一个使用C#开发的 通过deepseek api与deepseek对话的程序
## 使用教程
- 使用Visual Studio 2022打开
- 构建程序
- 在程序所在目录创建config.json 并输入如下内容：
  ```json
  {
   "apiKey": "your-api-key-here",
   "maxInputLength": 500,
   "maxOutputTokens": 1000
  }
  ```
- apiKey : 你的deepseek apikey
- maxInputLength : 最高输入的字符数
- maxOutputTokens : 最高输出token 超过的部分将不会输出
## 程序问题
- 不支持读取上下文
- 错误处理机制不完善
