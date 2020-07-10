# VAT ID Checker

![Build](https://github.com/software-architects/vat-id-check/workflows/Deploy%20VAT%20ID%20Checker%20to%20Azure%20Function%20App/badge.svg)

## Introduction

TODO: Describe what this project does

## Billomat

TODO: Describe how this projects interacts with Billomat
Create Webhook
`Settings`-> `Administration` -> `Webhooks` -> `New Webook`

## Slack Slash Command

TODO: Describe how the Slack slash command works (incl. sample of how to call it)
To use our slack slash command you have to be in the same channel you configured with the `SLACKCHANNEL` parameter.
Just type `/vatchecker {UST_ID}`. 
Insert the UST_ID you want to check and press `Enter`.
The slackbot sends this UST_ID to the same eu_validation program as before and slack displays the `UST_ID`, `company name`, `company address`

e.g. 
![SlackBotSend](https://github.com/software-architects/vat-id-check/blob/master/img/slackbotsend.png)
![SlackBotReceive](https://github.com/software-architects/vat-id-check/blob/master/img/slackbotreceive.png)

## Configuration Parameters

TODO: Describe configuration parameters. Please include links to relevant documentation of Billomat and Slack.

| Parameter                | Description |
| ------------             | ----------- |
| `BILLOMATID`             | To get your billomat ID, just copy your `yourbillomatid`, e.g `https://yourbillomatid.billomat.net/`        |
| `APIKEY`                 | To enable your api-key in Billomat go to `Settings`-> `Administration` -> `User` -> `Edit` -> `Enable API Access` -> `Show API Key`|
| `SLACKAUTHORIZATIONKEY`  | To be able to send messages to slack create a slack api token `Bearer xoxb-....`        |
| `SENDMESSAGEONSUCCESS`   | `true or false` If true, you receive a message even if everything's correct. If false, you won't receive a message upon success        |
| `SLACKCHANNEL`           | The channel you want to receive the message or write with the vat-id-checker-bot         |
| `SLACKUSER`              | Your user ID. Got to `Show Profile` -> `Click on the 3 Dots` -> `Copy your User ID`, e.g `U02FJAF8B`      |
| `relevant Links`         | Billomat ID: `https://www.billomat.com/support/faq/einstellungen/erklaerung-der-billomat-id/` 
api-key: `https://www.billomat.com/support/faq/schnittstellen-add-ons/api-schluessel-finden/` 
slack token: `https://api.slack.com/authentication/token-types#granular_bot` 
slack user ID: `https://help.workast.com/hc/en-us/articles/360027461274-How-to-find-a-Slack-user-ID`|
