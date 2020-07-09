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

## Configuration Parameters

TODO: Describe configuration parameters. Please include links to relevant documentation of Billomat and Slack.

| Parameter                | Description |
| ------------             | ----------- |
| `BILLOMATID`             | To get your BillomatID, just Copy your text from the URL in {}, e.g `https://{softarchmelinatest}.billomat.net/`        |
| `APIKEY`                 | To Enable your Api Key in Billomat go to `Settings`-> `Administration` -> `User` -> `Edit` -> `Enable API Access` -> `Show API Key`|
| `SLACKAUTHORIZATIONKEY`  | To be able to send Messages to slack `Bearer xoxb-2528351280-1235255009076-iJPMJx72RFblYyYoGM5ryMAH`        |
| `SENDMESSAGEONSUCCESS`   | `true or false` If true, you receive a message even if everything's correct. If false, you won't receive a message upon success        |
| `SLACKCHANNEL`           | The channel you want to receive the message or write with the Vat-ID-Checker-Bot         |
| `SLACKUSER`              | Your User ID. Got to `Show Profile` -> `Click on the 3 Dots` -> `Copy your User ID`, e.g `U02FJAF8B`      |
