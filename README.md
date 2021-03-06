# VAT ID Checker

![Build](https://github.com/software-architects/vat-id-check/workflows/Deploy%20VAT%20ID%20Checker%20to%20Azure%20Function%20App/badge.svg)

## Introduction

This project ckecks the correctness of the data you use to create an invoice via Billomat.<br>
If the `UST_ID`, `company name`, `company address` are not correct, for example if you have a typo, you receive a message via slack in an certain channel you can configure<br>
If the data is correct you will receive an `OK` message.<br>
If you don't want to, you can enable it in the configuration.<br>

![VatCheckReceive](https://github.com/software-architects/vat-id-check/blob/master/img/vatcheckreceive.png)

### How does it work?
Basically you use a Billomat Webhook to send data from a certain event (invoice create) to our program, which sends the data to a eu_validation program and validates the received data with the user's inserted one.<br>

## Billomat

To create a Webhook go to
`Settings`-> `Administration` -> `Webhooks` -> `New Webook`<br>
Insert in the URL slot this URL `https://vatidchecker.azurewebsites.net/api/VatIDCheck` and the `application/json` format.<br>

A Webhook is used to invoke the inserted URL, if an event happens. In our case it's if you create an invoice.

## Slack Slash Command

To use our slack slash command you have to be in the same channel you configured with the `SLACKCHANNEL` parameter.<br>
Just type `/vatchecker {UST_ID}`. <br>
Insert the UST_ID you want to check and press `Enter`.<br>
The bot sends this UST_ID to the same eu_validation program as before and slack displays the `UST_ID`, `company name` and `company address`<br>

e.g. <br>
![SlackBotSend](https://github.com/software-architects/vat-id-check/blob/master/img/slackbotsend.png)<br>
![SlackBotReceive](https://github.com/software-architects/vat-id-check/blob/master/img/slackbotreceive.png)

## Configuration Parameters


| Parameter                | Description |
| ------------             | ----------- |
| `BILLOMATID`             | To get your billomat ID, just copy your `yourbillomatid`, e.g `https://yourbillomatid.billomat.net/`        |
| `APIKEY`                 | To enable your api-key in Billomat go to `Settings`-> `Administration` -> `User` -> `Edit` -> `Enable API Access` -> `Show API Key`|
| `SLACKAUTHORIZATIONKEY`  | To be able to send messages to slack create a slack api token `Bearer xoxb-....`        |
| `SENDMESSAGEONSUCCESS`   | `true or false` If true, you receive a message even if everything's correct. If false, you won't receive a message upon success        |
| `SLACKCHANNEL`           | The channel you want to receive the message or write with the vat-id-checker-bot         |
| `SLACKUSER`              | To be marked in the message. Got to `Show Profile` -> `Click on the 3 Dots` -> `Copy your User ID`, e.g `U02FJAF8B`      |

## Relevant Links     
+ Billomat ID: https://www.billomat.com/support/faq/einstellungen/erklaerung-der-billomat-id/ 
+ Api-key: https://www.billomat.com/support/faq/schnittstellen-add-ons/api-schluessel-finden/ 
+ Slack token: https://api.slack.com/authentication/token-types#granular_bot
+ Slack user ID: https://help.workast.com/hc/en-us/articles/360027461274-How-to-find-a-Slack-user-ID
