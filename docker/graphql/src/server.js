const http = require("http");
const { postgraphile } = require("postgraphile");

const e = process.env;

const postgraphileCommonOptions = {
  appendPlugins: [require("@graphile-contrib/pg-simplify-inflector")],

  setofFunctionsContainNulls: false,
  subscriptions: true,
  dynamicJson: true,
  ignoreRBAC: false,
  ignoreIndexes: false,
  legacyRelations: "omit",

  jwtSecret: e.JWT_SECRET,
  jwtPgTypeIdentifier: e.JWT_PG_TYPE,
  pgDefaultRole: e.DEFAULT_ROLE,
};

const postgraphileDevelopmentOptions = {
  ...postgraphileCommonOptions,
  watchPg: true,
  showErrorStack: "json",
  extendedErrors: ["hint", "detail", "errcode"],
  exportGqlSchemaPath: "schema.graphql",
  graphiql: true,
  enhanceGraphiql: true,
  allowExplain(req) {
    return true;
  },
  enableQueryBatching: true,
};

const postgraphileProductionOptions = {
  ...postgraphileCommonOptions,
  retryOnInitFail: true,
  extendedErrors: ["errcode"],
  graphiql: false,
  enableQueryBatching: true,
  disableQueryLog: true,
};

http
  .createServer(
    postgraphile(
      e.DATABASE_URL,
      e.SCHEMA,
      e.MODE === "production"
        ? postgraphileProductionOptions
        : postgraphileDevelopmentOptions
    )
  )
  .listen(e.PORT);
